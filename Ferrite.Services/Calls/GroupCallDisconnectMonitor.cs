// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Calls;

public sealed class GroupCallDisconnectOptions
{
    public TimeSpan Grace { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan ConnectGrace { get; init; } = TimeSpan.FromSeconds(30);
}

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

    public long EvictedCount => Interlocked.Read(ref _evicted);

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

    public void OnDisconnect(GroupCallMediaDisconnectEvent disconnect)
    {
        var key = (disconnect.CallId, disconnect.ParticipantId);
        lock (_gate)
        {
            if (_subscription == null || _pending.ContainsKey(key))
            {
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
