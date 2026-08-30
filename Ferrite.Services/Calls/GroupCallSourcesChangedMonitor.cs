// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Calls;

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

    public async Task<bool> RefreshAsync(GroupCallMediaSourcesChangedEvent changed)
    {
        try
        {
            return await RefreshCoreAsync(changed);
        }
        catch (GroupCallMediaException e)
        {
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

        IReadOnlyList<long> members = await ReadConferenceMembersAsync(callId);
        return members.Count == 0
            ? 0
            : await _fanout.PushGroupCallUpdatesToAsync(members, BuildForMember);
    }

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
