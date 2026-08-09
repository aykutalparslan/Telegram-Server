// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Calls;

public sealed class GroupCallDisconnectOptions
{
    /// <summary>
    /// How long a participant may be disconnected from the media worker before
    /// Ferrite marks it left. Long enough to survive an ICE restart or a brief
    /// network change, short enough that the other members stop seeing a
    /// participant who is not coming back.
    /// </summary>
    public TimeSpan Grace { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long after joining a participant still counts as live even though the
    /// worker has no connected transport for it yet. ICE and DTLS complete after
    /// the join answer, so a participant is legitimately "not connected yet" for
    /// a while; reporting that as dropped in <c>phone.checkGroupCall</c> makes
    /// pinned TDLib conclude it lost the call and leave. Eviction of a transport
    /// that never arrives stays with <see cref="Grace"/>.
    /// </summary>
    public TimeSpan ConnectGrace { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Turns worker disconnect events into participant evictions, but only after a
/// grace period the participant can recover from.
///
/// A transport closing is not the same as leaving: a client that reconnects or
/// restarts ICE re-establishes the same media_id, so the eviction is cancelled by
/// re-checking liveness at expiry rather than by trusting the event. Only when
/// the participant is still unreachable does it commit the same versioned left
/// row an explicit <c>leaveGroupCall</c> would have written.
/// </summary>
public sealed class GroupCallDisconnectMonitor : IDisposable
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    private readonly IGroupCallMediaPlane _media;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateFanout _fanout;
    private readonly GroupCallMediaSourceMap _sourceMap;
    private readonly GroupCallDisconnectOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _log;
    private readonly object _gate = new();
    private readonly Dictionary<(long CallId, string MediaId), ITimer> _pending = new();
    private IDisposable? _subscription;
    private long _evicted;
    private long _recovered;

    public GroupCallDisconnectMonitor(IGroupCallMediaPlane media, IUnitOfWork unitOfWork, IGroupCallsRepository groupCallsRepository,
        UpdateFanout fanout, GroupCallMediaSourceMap sourceMap,
        GroupCallDisconnectOptions options, TimeProvider timeProvider, ILogger log)
    {
        _groupCallsRepository = groupCallsRepository;

        _media = media;
        _unitOfWork = unitOfWork;
        _fanout = fanout;
        _sourceMap = sourceMap;
        _options = options;
        _timeProvider = timeProvider;
        _log = log;
    }

    /// <summary>Participants marked left because their grace period expired.</summary>
    public long EvictedCount => Interlocked.Read(ref _evicted);

    /// <summary>Disconnects whose participant was alive again by expiry.</summary>
    public long RecoveredCount => Interlocked.Read(ref _recovered);

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _subscription ??= _media.Subscribe(OnDisconnect);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        List<ITimer> timers;
        lock (_gate)
        {
            _subscription?.Dispose();
            _subscription = null;
            timers = _pending.Values.ToList();
            _pending.Clear();
        }

        foreach (ITimer timer in timers)
        {
            timer.Dispose();
        }
    }

    /// <summary>
    /// The media plane's disconnect callback. Public so a caller (and a test) can
    /// drive the grace period without going through a live subscription.
    /// </summary>
    public void OnDisconnect(GroupCallMediaDisconnectEvent disconnect)
    {
        var key = (disconnect.CallId, disconnect.ParticipantId);
        lock (_gate)
        {
            if (_subscription == null || _pending.ContainsKey(key))
            {
                // Already counting down; a repeated event must not reset the clock,
                // or a flapping transport would postpone eviction forever.
                return;
            }

            _pending[key] = _timeProvider.CreateTimer(
                _ => _ = ExpireAsync(disconnect), null, _options.Grace,
                Timeout.InfiniteTimeSpan);
        }

        _log.Debug($"📞 GroupCall disconnect call:{disconnect.CallId} " +
                   $"media:{disconnect.ParticipantId} reason:{disconnect.Reason}; " +
                   $"grace {_options.Grace.TotalSeconds:0}s");
    }

    /// <summary>
    /// The grace decision itself, separated from the timer that normally fires it
    /// so eviction and recovery are decidable without waiting. Returns true when
    /// the participant was marked left.
    /// </summary>
    public async Task<bool> ExpireAsync(GroupCallMediaDisconnectEvent disconnect)
    {
        var key = (disconnect.CallId, disconnect.ParticipantId);
        lock (_gate)
        {
            if (_pending.Remove(key, out ITimer? timer))
            {
                timer.Dispose();
            }
        }

        try
        {
            // A worker that died takes every transport with it, so there is nothing
            // to recover to; any other reason gets one last liveness check, which is
            // what makes a reconnect or a rejoin cancel the eviction.
            if (disconnect.Reason != GroupCallMediaDisconnectReason.WorkerDied &&
                await _media.IsAliveAsync(disconnect.CallId, disconnect.ParticipantId))
            {
                Interlocked.Increment(ref _recovered);
                _log.Debug($"📞 GroupCall disconnect recovered call:{disconnect.CallId} " +
                           $"media:{disconnect.ParticipantId}");
                return false;
            }
        }
        catch (GroupCallMediaException e)
        {
            _log.Warning(e, $"📞 GroupCall disconnect could not re-check liveness for " +
                            $"call:{disconnect.CallId} media:{disconnect.ParticipantId}; " +
                            $"treating it as gone");
        }

        return await EvictAsync(disconnect);
    }

    private async Task<bool> EvictAsync(GroupCallMediaDisconnectEvent disconnect)
    {
        long userId;
        long peerChatId;
        using (TLDto.TLGroupCallState? call = await _groupCallsRepository
                   .GetCallAsync(disconnect.CallId))
        {
            if (call == null ||
                call.Value.AsGroupCallState().State !=
                (int)GroupCallPersistenceState.Active)
            {
                return false;
            }
            peerChatId = call.Value.AsGroupCallState().PeerId;
        }

        // The event names a media_id, so the row it belongs to is resolved rather
        // than assumed: a participant that rejoined under a different id must not
        // be evicted by a stale event.
        long? owner = await FindParticipantAsync(disconnect.CallId,
            disconnect.ParticipantId);
        if (owner == null)
        {
            return false;
        }
        userId = owner.Value;

        GroupCallLeaveResult left = await _groupCallsRepository
            .TryLeaveParticipantAsync(disconnect.CallId, userId);
        if (left.Status != GroupCallLeaveStatus.Left)
        {
            left.Participant?.Dispose();
            left.Call?.Dispose();
            return false;
        }

        await _unitOfWork.SaveAsync();
        _sourceMap.RemoveParticipant(disconnect.CallId, disconnect.ParticipantId);
        Interlocked.Increment(ref _evicted);

        using TLDto.TLGroupCallParticipantState participant = left.Participant!.Value;
        using TLDto.TLGroupCallState updatedCall = left.Call!.Value;
        int delivered = await PushLeftRowAsync(updatedCall, participant, peerChatId);

        _log.Debug($"📞 GroupCall evicted call:{disconnect.CallId} user:{userId} " +
                   $"media:{disconnect.ParticipantId} members:{delivered}");
        return true;
    }

    private async ValueTask<long?> FindParticipantAsync(long callId, string mediaId)
    {
        GroupCallParticipantPage page = await _groupCallsRepository
            .GetParticipantsPageAsync(callId, offset: null, limit: int.MaxValue);
        try
        {
            foreach (TLDto.TLGroupCallParticipantState state in page.Participants)
            {
                var view = state.AsGroupCallParticipantState();
                if (!view.Left && Encoding.UTF8.GetString(view.MediaId) == mediaId)
                {
                    return view.UserId;
                }
            }
        }
        finally
        {
            foreach (TLDto.TLGroupCallParticipantState state in page.Participants)
            {
                state.Dispose();
            }
        }

        return null;
    }

    /// <summary>
    /// The same versioned left row an explicit leave would have produced, so a
    /// client cannot tell an eviction from a normal departure and needs no extra
    /// recovery path. A left row carries no media, so one payload serves every
    /// member.
    /// </summary>
    private async Task<int> PushLeftRowAsync(TLDto.TLGroupCallState call,
        TLDto.TLGroupCallParticipantState participant, long peerChatId)
    {
        var view = call.AsGroupCallState();
        long callId = view.Id;
        int version = view.Version;
        byte[] inputCallBytes;
        using (TLInputGroupCall inputCall = GroupCallBuilders.BuildInputGroupCall(call))
        {
            inputCallBytes = inputCall.AsSpan().ToArray();
        }

        return await _fanout.PushGroupCallUpdatesAsync(peerChatId, excludeUserId: null,
            memberId =>
            {
                using TLGroupCallParticipant row = GroupCallBuilders.BuildParticipant(
                    participant,
                    new GroupCallViewer(memberId, CanManageCall: false,
                        ScheduleStartSubscribed: false),
                    GroupCallParticipantOverlay.None,
                    GroupCallParticipantDecoration.Versioned);
                var participants = new Vector();
                participants.AppendTLObject(row.AsSpan());
                return Task.FromResult<TLUpdate?>(UpdateGroupCallParticipants.Builder()
                    .Call(inputCallBytes)
                    .Participants(participants)
                    .Version(version)
                    .Build());
            });
    }
}
