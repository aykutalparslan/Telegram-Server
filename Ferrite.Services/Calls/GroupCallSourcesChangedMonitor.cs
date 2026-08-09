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

/// <summary>
/// Re-publishes a call's per-viewer media mapping after the WORKER changed it on
/// its own.
///
/// The only such change today is a video codec correction. A join answer offers
/// every codec, the client picks one and never tells us which, and the worker
/// only finds out from the RTP that arrives — at which point it re-creates the
/// producer and every consumer of it. Consumer SSRCs are rewritten per viewer,
/// so every peer that already received this participant's row is now listening
/// on sources that carry nothing. Without this refresh a client whose codec had
/// to be corrected is heard but never seen.
///
/// The refresh is deliberately version-free: nothing about the participant's
/// state changed, only the SSRCs it is advertised on, and consuming a call
/// version here would make every client resync its whole participant list.
/// Pinned TDLib applies a participant row at the call's current version, which
/// is the same thing a viewer-local mute edit relies on.
/// </summary>
public sealed class GroupCallSourcesChangedMonitor : IDisposable
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    private readonly IGroupCallMediaPlane _media;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateFanout _fanout;
    private readonly GroupCallMediaSourceMap _sourceMap;
    private readonly ILogger _log;
    private readonly object _gate = new();
    private IDisposable? _subscription;
    private long _refreshed;

    public GroupCallSourcesChangedMonitor(IGroupCallMediaPlane media,
        IUnitOfWork unitOfWork, IGroupCallsRepository groupCallsRepository, UpdateFanout fanout,
        GroupCallMediaSourceMap sourceMap, ILogger log)
    {
        _groupCallsRepository = groupCallsRepository;

        _media = media;
        _unitOfWork = unitOfWork;
        _fanout = fanout;
        _sourceMap = sourceMap;
        _log = log;
    }

    /// <summary>Mappings re-read and re-published.</summary>
    public long RefreshedCount => Interlocked.Read(ref _refreshed);

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _subscription ??= _media.SubscribeSourcesChanged(
                changed => _ = RefreshAsync(changed));
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
        lock (_gate)
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }

    /// <summary>
    /// Re-read the worker's mapping and re-publish the affected participant.
    /// Public so a test can drive it without a live subscription. Returns true
    /// when a row was actually re-published.
    /// </summary>
    public async Task<bool> RefreshAsync(GroupCallMediaSourcesChangedEvent changed)
    {
        try
        {
            return await RefreshCoreAsync(changed);
        }
        catch (GroupCallMediaException e)
        {
            // The worker is unreachable or the room is gone. The stale mapping
            // stays, which degrades that participant's video rows; the next join
            // in this call replaces the whole mapping anyway.
            _log.Warning(e, $"📞 GroupCall sources refresh failed call:{changed.CallId} " +
                            $"media:{changed.ParticipantId}");
            return false;
        }
    }

    private async Task<bool> RefreshCoreAsync(GroupCallMediaSourcesChangedEvent changed)
    {
        long peerId;
        bool conference;
        using (TLDto.TLGroupCallState? call = await _groupCallsRepository
                   .GetCallAsync(changed.CallId))
        {
            if (call == null ||
                call.Value.AsGroupCallState().State !=
                (int)GroupCallPersistenceState.Active)
            {
                return false;
            }
            var view = call.Value.AsGroupCallState();
            peerId = view.PeerId;
            conference = view.Conference;
        }

        var sources = await _media.ReadViewerSourcesAsync(changed.CallId);
        _sourceMap.Replace(changed.CallId, sources);

        // The event names a media_id; the row that owns it decides whose
        // participant row gets re-sent. A participant that already left is
        // simply not re-published.
        long? owner = await FindParticipantAsync(changed.CallId, changed.ParticipantId);
        if (owner == null)
        {
            return false;
        }

        int delivered = await PublishAsync(changed.CallId, owner.Value,
            changed.ParticipantId, peerId, conference);
        Interlocked.Increment(ref _refreshed);
        _log.Debug($"📞 GroupCall sources refreshed call:{changed.CallId} " +
                   $"user:{owner.Value} media:{changed.ParticipantId} " +
                   $"reason:{changed.Reason} members:{delivered}");
        return true;
    }

    private async Task<int> PublishAsync(long callId, long producerUserId,
        string producerMediaId, long peerId, bool conference)
    {
        using TLDto.TLGroupCallState? call = await _groupCallsRepository
            .GetCallAsync(callId);
        using TLDto.TLGroupCallParticipantState? participant = await _groupCallsRepository.GetParticipantAsync(callId, producerUserId);
        if (call == null || participant == null)
        {
            return 0;
        }

        TLDto.TLGroupCallState callRow = call.Value;
        TLDto.TLGroupCallParticipantState participantRow = participant.Value;

        Task<TLUpdate?> BuildForMember(long memberId) =>
            BuildRowAsync(callId, callRow, participantRow, memberId,
                producerUserId, producerMediaId);

        if (!conference)
        {
            return await _fanout.PushGroupCallUpdatesAsync(peerId,
                excludeUserId: null, BuildForMember);
        }

        // A conference is peerless: its own participant list is the audience.
        IReadOnlyList<long> members = await ReadConferenceMembersAsync(callId);
        return members.Count == 0
            ? 0
            : await _fanout.PushGroupCallUpdatesToAsync(members, BuildForMember);
    }

    /// <summary>
    /// One receiver's view of the corrected row. Every viewer sees different
    /// SSRCs, and each keeps its own local mute/volume overlay, which a refresh
    /// must not reset.
    /// </summary>
    private async Task<TLUpdate?> BuildRowAsync(long callId,
        TLDto.TLGroupCallState call, TLDto.TLGroupCallParticipantState participant,
        long memberId, long producerUserId, string producerMediaId)
    {
        string? viewerMediaId = await GetMediaIdAsync(callId, memberId);
        bool mutedByYou = false;
        int? localVolume = null;
        using (TLDto.TLGroupCallViewerParticipantState? local = await _groupCallsRepository.GetViewerParticipantStateAsync(callId,
                       memberId, producerUserId))
        {
            if (local != null)
            {
                var view = local.Value.AsGroupCallViewerParticipantState();
                mutedByYou = view.MutedByYou;
                localVolume = view.Flags[1] ? view.Volume : null;
            }
        }

        GroupCallViewerSources? viewerSources = _sourceMap.TryGet(callId,
            viewerMediaId, producerMediaId);
        var overlay = new GroupCallParticipantOverlay(mutedByYou, localVolume,
            viewerSources?.AudioSource ?? 0, viewerSources);

        using TLGroupCallParticipant row = GroupCallBuilders.BuildParticipant(
            participant,
            new GroupCallViewer(memberId, CanManageCall: false,
                ScheduleStartSubscribed: false),
            overlay, GroupCallParticipantDecoration.Versioned);

        using TLInputGroupCall inputCall = GroupCallBuilders.BuildInputGroupCall(call);
        var participants = new Vector();
        participants.AppendTLObject(row.AsSpan());
        // inputCall stays alive through Build(), so the builder reads its span
        // directly rather than laundering a pooled value into a managed array.
        return UpdateGroupCallParticipants.Builder()
            .Call(inputCall.AsSpan())
            .Participants(participants)
            .Version(call.AsGroupCallState().Version)
            .Build();
    }

    private async ValueTask<string?> GetMediaIdAsync(long callId, long userId)
    {
        using TLDto.TLGroupCallParticipantState? participant = await _groupCallsRepository.GetParticipantAsync(callId, userId);
        if (participant == null)
        {
            return null;
        }
        var view = participant.Value.AsGroupCallParticipantState();
        return view.Left ? null : Encoding.UTF8.GetString(view.MediaId);
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

    private async ValueTask<IReadOnlyList<long>> ReadConferenceMembersAsync(long callId)
    {
        GroupCallParticipantPage page = await _groupCallsRepository
            .GetParticipantsPageAsync(callId, offset: null, limit: int.MaxValue);
        var members = new List<long>();
        try
        {
            foreach (TLDto.TLGroupCallParticipantState state in page.Participants)
            {
                var view = state.AsGroupCallParticipantState();
                if (!view.Left)
                {
                    members.Add(view.UserId);
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

        return members;
    }
}
