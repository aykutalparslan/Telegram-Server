// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using TLDto = Ferrite.TL.baseLayer.dto;
using Ferrite.Data.Models;

namespace Ferrite.Data.Repositories;

public sealed class GroupCallsRepository : IGroupCallsRepository
{
    private const int StripeCount = 256;
    private const string OffsetPrefix = "gcp1:";

    private readonly IKVStore _calls;
    private readonly IKVStore _activePeers;
    private readonly IKVStore _participants;
    private readonly IKVStore _viewerStates;
    private readonly IKVStore _viewerParticipantStates;
    private readonly IKVStore _defaultJoinAs;
    private readonly IKVStore _invites;
    private readonly Func<ValueTask<bool>> _flush;
    private readonly SemaphoreSlim[] _callGates = CreateGates();
    private readonly SemaphoreSlim[] _peerGates = CreateGates();
    private readonly SemaphoreSlim _inviteGate = new(1, 1);

    public GroupCallsRepository(IKVStore calls, IKVStore activePeers, IKVStore participants,
        IKVStore viewerStates, IKVStore viewerParticipantStates, IKVStore defaultJoinAs,
        IKVStore invites, Func<ValueTask<bool>>? flush = null)
    {
        _calls = calls;
        calls.SetSchema(new TableDefinition("ferrite", "group_calls",
            new KeyDefinition("pk",
                new DataColumn { Name = "call_id", Type = DataType.Long },
                new DataColumn { Name = "peer_type", Type = DataType.Int },
                new DataColumn { Name = "peer_id", Type = DataType.Long },
                new DataColumn { Name = "random_id", Type = DataType.Int }),
            new KeyDefinition("by_peer_random",
                new DataColumn { Name = "peer_type", Type = DataType.Int },
                new DataColumn { Name = "peer_id", Type = DataType.Long },
                new DataColumn { Name = "random_id", Type = DataType.Int })));
        _activePeers = activePeers;
        activePeers.SetSchema(new TableDefinition("ferrite", "group_call_active_peers",
            new KeyDefinition("pk",
                new DataColumn { Name = "peer_type", Type = DataType.Int },
                new DataColumn { Name = "peer_id", Type = DataType.Long })));
        _participants = participants;
        participants.SetSchema(new TableDefinition("ferrite", "group_call_participants",
            new KeyDefinition("pk",
                new DataColumn { Name = "call_id", Type = DataType.Long },
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "join_date", Type = DataType.Int },
                new DataColumn { Name = "source", Type = DataType.Int }),
            new KeyDefinition("by_call_order",
                new DataColumn { Name = "call_id", Type = DataType.Long },
                new DataColumn { Name = "join_date", Type = DataType.Int },
                new DataColumn { Name = "user_id", Type = DataType.Long }),
            new KeyDefinition("by_source",
                new DataColumn { Name = "call_id", Type = DataType.Long },
                new DataColumn { Name = "source", Type = DataType.Int })));
        _viewerStates = viewerStates;
        viewerStates.SetSchema(new TableDefinition("ferrite", "group_call_viewer_states",
            new KeyDefinition("pk",
                new DataColumn { Name = "call_id", Type = DataType.Long },
                new DataColumn { Name = "user_id", Type = DataType.Long })));
        _viewerParticipantStates = viewerParticipantStates;
        viewerParticipantStates.SetSchema(new TableDefinition("ferrite",
            "group_call_viewer_participant_states",
            new KeyDefinition("pk",
                new DataColumn { Name = "call_id", Type = DataType.Long },
                new DataColumn { Name = "viewer_user_id", Type = DataType.Long },
                new DataColumn { Name = "target_user_id", Type = DataType.Long })));
        _defaultJoinAs = defaultJoinAs;
        defaultJoinAs.SetSchema(new TableDefinition("ferrite", "group_call_default_join_as",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "peer_type", Type = DataType.Int },
                new DataColumn { Name = "peer_id", Type = DataType.Long })));
        _invites = invites;
        invites.SetSchema(new TableDefinition("ferrite", "group_call_invites",
            new KeyDefinition("pk",
                new DataColumn { Name = "call_id", Type = DataType.Long },
                new DataColumn { Name = "hash", Type = DataType.String }),
            new KeyDefinition("by_hash",
                new DataColumn { Name = "hash", Type = DataType.String })));
        _flush = flush ?? (() => ValueTask.FromResult(true));
    }

    private static SemaphoreSlim[] CreateGates() =>
        Enumerable.Range(0, StripeCount).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    private static SemaphoreSlim GetGate(SemaphoreSlim[] gates, long key) =>
        gates[(int)(unchecked((ulong)key) % (uint)gates.Length)];

    private async ValueTask FlushAsync(string operation)
    {
        if (!await _flush())
        {
            throw new IOException($"Failed to persist {operation}.");
        }
    }

    private static TLDto.TLGroupCallState CloneCall(TLDto.TLGroupCallState call) =>
        call.AsGroupCallState().Clone().Build();

    private static TLDto.TLGroupCallParticipantState CloneParticipant(
        TLDto.TLGroupCallParticipantState participant) =>
        participant.AsGroupCallParticipantState().Clone().Build();

    private static TLDto.TLGroupCallState BumpCallVersion(TLDto.TLGroupCallState call,
        int participantsDelta)
    {
        var view = call.AsGroupCallState();
        return view.Clone()
            .ParticipantsCount(Math.Max(0, view.ParticipantsCount + participantsDelta))
            .Version(view.Version + 1)
            .Build();
    }

    private static TLDto.TLGroupCallState DiscardCallRow(TLDto.TLGroupCallState call,
        int endedDate, int duration)
    {
        var view = call.AsGroupCallState();
        var builder = TLDto.GroupCallState.Builder()
            .Id(view.Id)
            .AccessHash(view.AccessHash)
            .PeerType(view.PeerType)
            .PeerId(view.PeerId)
            .CreatorUserId(view.CreatorUserId)
            .RandomId(view.RandomId)
            .State((int)GroupCallPersistenceState.Discarded)
            .CreatedDate(view.CreatedDate)
            .EndedDate(endedDate)
            .Duration(duration)
            .ParticipantsCount(0)
            .Version(view.Version + 1)
            .InviteGeneration(view.InviteGeneration)
            .MediaEpoch(view.MediaEpoch);
        if (view.JoinMuted)
        {
            builder = builder.JoinMuted(true);
        }
        if (view.Flags[1])
        {
            builder = builder.Title(view.Title);
        }
        if (view.Flags[3])
        {
            builder = builder.StartedDate(view.StartedDate);
        }
        if (view.RtmpStream)
        {
            builder = builder.RtmpStream(true);
        }
        if (view.Flags[12])
        {
            builder = builder.RecordingGeneration(view.RecordingGeneration);
        }
        if (view.Conference)
        {
            builder = builder.Conference(true);
        }

        return builder.Build();
    }

    private static TLDto.TLGroupCallState StartScheduledCallRow(
        TLDto.TLGroupCallState call, int startedDate)
    {
        var view = call.AsGroupCallState();
        var builder = TLDto.GroupCallState.Builder()
            .Id(view.Id)
            .AccessHash(view.AccessHash)
            .PeerType(view.PeerType)
            .PeerId(view.PeerId)
            .CreatorUserId(view.CreatorUserId)
            .RandomId(view.RandomId)
            .State((int)GroupCallPersistenceState.Active)
            .CreatedDate(view.CreatedDate)
            .StartedDate(startedDate)
            .Version(view.Version + 1)
            .ParticipantsCount(view.ParticipantsCount)
            .InviteGeneration(view.InviteGeneration)
            .MediaEpoch(view.MediaEpoch);
        if (view.JoinMuted)
        {
            builder = builder.JoinMuted(true);
        }
        if (view.Flags[1])
        {
            builder = builder.Title(view.Title);
        }
        if (view.RtmpStream)
        {
            builder = builder.RtmpStream(true);
        }
        if (view.Conference)
        {
            builder = builder.Conference(true);
        }

        return builder.Build();
    }

    private static TLDto.TLGroupCallState BuildRecordingCallRow(
        TLDto.TLGroupCallState call, bool start, int startDate,
        long initiatingUserId, string title, bool video, bool portrait,
        int generation)
    {
        var view = call.AsGroupCallState();
        var builder = TLDto.GroupCallState.Builder()
            .Id(view.Id)
            .AccessHash(view.AccessHash)
            .PeerType(view.PeerType)
            .PeerId(view.PeerId)
            .CreatorUserId(view.CreatorUserId)
            .RandomId(view.RandomId)
            .State(view.State)
            .CreatedDate(view.CreatedDate)
            .Version(view.Version)
            .ParticipantsCount(view.ParticipantsCount)
            .InviteGeneration(view.InviteGeneration)
            .MediaEpoch(view.MediaEpoch)
            .RecordingGeneration(generation);
        if (view.JoinMuted)
        {
            builder = builder.JoinMuted(true);
        }
        if (view.Flags[1])
        {
            builder = builder.Title(view.Title);
        }
        if (view.Flags[2])
        {
            builder = builder.ScheduleDate(view.ScheduleDate);
        }
        if (view.Flags[3])
        {
            builder = builder.StartedDate(view.StartedDate);
        }
        if (view.Flags[4])
        {
            builder = builder.EndedDate(view.EndedDate);
        }
        if (view.Flags[5])
        {
            builder = builder.Duration(view.Duration);
        }
        if (view.RtmpStream)
        {
            builder = builder.RtmpStream(true);
        }
        if (view.Conference)
        {
            builder = builder.Conference(true);
        }
        if (start)
        {
            builder = builder
                .RecordStartDate(startDate)
                .RecordingUserId(initiatingUserId);
            if (video)
            {
                builder = builder.RecordVideoActive(true);
                if (portrait)
                {
                    builder = builder.RecordVideoPortrait(true);
                }
            }
            if (!string.IsNullOrEmpty(title))
            {
                builder = builder.RecordingTitle(Encoding.UTF8.GetBytes(title));
            }
        }

        return builder.Build();
    }

    private static TLDto.TLGroupCallParticipantState MarkParticipantLeft(
        TLDto.TLGroupCallParticipantState participant)
    {
        var view = participant.AsGroupCallParticipantState();
        return view.Clone()
            .Left(true)
            .Build();
    }

    private static TLDto.TLGroupCallParticipantState BuildEditedParticipant(
        TLDto.TLGroupCallParticipantState participant, GroupCallParticipantEditSpec edit)
    {
        var view = participant.AsGroupCallParticipantState();
        var builder = TLDto.GroupCallParticipantState.Builder()
            .CallId(view.CallId)
            .UserId(view.UserId)
            .PeerType(view.PeerType)
            .PeerId(view.PeerId)
            .JoinDate(view.JoinDate)
            .Source(view.Source)
            .MediaId(view.MediaId);
        if (edit.Muted ?? view.Muted)
        {
            builder = builder.Muted(true);
        }
        if (edit.CanSelfUnmute ?? view.CanSelfUnmute)
        {
            builder = builder.CanSelfUnmute(true);
        }
        if (view.Left)
        {
            builder = builder.Left(true);
        }
        if (view.Flags[3])
        {
            builder = builder.ActiveDate(view.ActiveDate);
        }
        if (edit.Volume is { } volume)
        {
            builder = builder.Volume(volume);
        }
        else if (view.Flags[4])
        {
            builder = builder.Volume(view.Volume);
        }
        if (!edit.ClearRaiseHand)
        {
            if (edit.RaiseHandRating is { } rating)
            {
                builder = builder.RaiseHandRating(rating);
            }
            else if (view.Flags[5])
            {
                builder = builder.RaiseHandRating(view.RaiseHandRating);
            }
        }
        if (view.Flags[6])
        {
            builder = builder.About(view.About);
        }
        if (edit.VideoStopped ?? view.VideoStopped)
        {
            builder = builder.VideoStopped(true);
        }
        if (edit.VideoPaused ?? view.VideoPaused)
        {
            builder = builder.VideoPaused(true);
        }
        if (edit.PresentationPaused ?? view.PresentationPaused)
        {
            builder = builder.PresentationPaused(true);
        }
        if (edit.VideoJoined ?? view.VideoJoined)
        {
            builder = builder.VideoJoined(true);
        }
        if (view.Flags[11])
        {
            builder = builder.VideoEndpoint(view.VideoEndpoint);
        }
        if (view.Flags[12])
        {
            builder = builder.PresentationEndpoint(view.PresentationEndpoint);
        }

        return builder.Build();
    }

    private static TLDto.TLGroupCallParticipantState RebuildParticipant(
        TLDto.TLGroupCallParticipantState participant, string? presentationEndpoint)
    {
        var view = participant.AsGroupCallParticipantState();
        var builder = TLDto.GroupCallParticipantState.Builder()
            .CallId(view.CallId)
            .UserId(view.UserId)
            .PeerType(view.PeerType)
            .PeerId(view.PeerId)
            .JoinDate(view.JoinDate)
            .Source(view.Source)
            .MediaId(view.MediaId);
        if (view.Muted)
        {
            builder = builder.Muted(true);
        }
        if (view.CanSelfUnmute)
        {
            builder = builder.CanSelfUnmute(true);
        }
        if (view.Left)
        {
            builder = builder.Left(true);
        }
        if (view.Flags[3])
        {
            builder = builder.ActiveDate(view.ActiveDate);
        }
        if (view.Flags[4])
        {
            builder = builder.Volume(view.Volume);
        }
        if (view.Flags[5])
        {
            builder = builder.RaiseHandRating(view.RaiseHandRating);
        }
        if (view.Flags[6])
        {
            builder = builder.About(view.About);
        }
        if (view.VideoStopped)
        {
            builder = builder.VideoStopped(true);
        }
        if (view.VideoPaused)
        {
            builder = builder.VideoPaused(true);
        }
        if (view.PresentationPaused)
        {
            builder = builder.PresentationPaused(true);
        }
        if (view.VideoJoined)
        {
            builder = builder.VideoJoined(true);
        }
        if (view.Flags[11])
        {
            builder = builder.VideoEndpoint(view.VideoEndpoint);
        }
        if (presentationEndpoint != null)
        {
            builder = builder.PresentationEndpoint(
                Encoding.UTF8.GetBytes(presentationEndpoint));
        }

        return builder.Build();
    }

    private static TLDto.TLGroupCallParticipantState TouchParticipant(
        TLDto.TLGroupCallParticipantState participant, int activeDate)
    {
        var view = participant.AsGroupCallParticipantState();
        return view.Clone()
            .ActiveDate(activeDate)
            .Build();
    }

    private void PutCall(TLDto.TLGroupCallState call)
    {
        var view = call.AsGroupCallState();
        _calls.Put(call.AsSpan().ToArray(), view.Id, view.PeerType, view.PeerId,
            view.RandomId);
    }

    private void PutParticipant(TLDto.TLGroupCallParticipantState participant)
    {
        var view = participant.AsGroupCallParticipantState();
        int sourceKey = view.Left ? 0 : view.Source;
        _participants.Put(participant.AsSpan().ToArray(), view.CallId, view.UserId,
            view.JoinDate, sourceKey);
    }

    private static TLDto.TLGroupCallState ReadCall(byte[] bytes) =>
        new(bytes, 0, bytes.Length);

    private static TLDto.TLGroupCallParticipantState ReadParticipant(byte[] bytes) =>
        new(bytes, 0, bytes.Length);

    private async ValueTask<TLDto.TLGroupCallState?> GetCallInternalAsync(long callId,
        CancellationToken cancellationToken)
    {
        await foreach (byte[] bytes in _calls.IterateAsync(callId)
                           .WithCancellation(cancellationToken))
        {
            return ReadCall(bytes);
        }
        return null;
    }

    private async ValueTask<TLDto.TLGroupCallParticipantState?> GetParticipantInternalAsync(
        long callId, long userId, CancellationToken cancellationToken)
    {
        await foreach (byte[] bytes in _participants.IterateAsync(callId, userId)
                           .WithCancellation(cancellationToken))
        {
            return ReadParticipant(bytes);
        }
        return null;
    }

    public ValueTask<GroupCallCreateResult> TryCreateCallAsync(
        TLDto.TLGroupCallState call, CancellationToken cancellationToken = default) =>
        CreateCallAsync(call, conference: false, cancellationToken);

    public ValueTask<GroupCallCreateResult> TryCreateConferenceCallAsync(
        TLDto.TLGroupCallState call, CancellationToken cancellationToken = default) =>
        CreateCallAsync(call, conference: true, cancellationToken);

    private async ValueTask<GroupCallCreateResult> CreateCallAsync(
        TLDto.TLGroupCallState call, bool conference, CancellationToken cancellationToken)
    {
        var view = call.AsGroupCallState();
        long callId = view.Id;
        int peerType = view.PeerType;
        long peerId = view.PeerId;
        int randomId = view.RandomId;
        if (view.Conference != conference)
        {
            throw new ArgumentException(
                "A conference row must be created through TryCreateConferenceCallAsync " +
                "and a hosted row through TryCreateCallAsync.", nameof(call));
        }
        SemaphoreSlim peerGate = GetGate(_peerGates, peerId);
        await peerGate.WaitAsync(cancellationToken);
        try
        {
            byte[]? duplicate = await _calls.GetBySecondaryIndexAsync("by_peer_random",
                peerType, peerId, randomId);
            if (duplicate != null)
            {
                return new GroupCallCreateResult(GroupCallCreateStatus.Idempotent,
                    ReadCall(duplicate));
            }
            using TLDto.TLGroupCallState? existing =
                await GetCallInternalAsync(callId, cancellationToken);
            if (existing != null)
            {
                return new GroupCallCreateResult(GroupCallCreateStatus.IdCollision, null);
            }
            (int activePeerType, long activePeerId) = ActiveCallKey(call);
            if (!conference)
            {
                byte[]? activePeer = await _activePeers.GetAsync(activePeerType,
                    activePeerId);
                if (activePeer != null)
                {
                    return new GroupCallCreateResult(
                        GroupCallCreateStatus.ActiveCallExists, null);
                }
            }
            PutCall(call);
            using TLDto.TLGroupCallActivePeer active = TLDto.GroupCallActivePeer.Builder()
                .PeerType(activePeerType)
                .PeerId(activePeerId)
                .CallId(callId)
                .Build();
            _activePeers.Put(active.AsSpan().ToArray(), activePeerType, activePeerId);
            await FlushAsync("group call creation");
            return new GroupCallCreateResult(GroupCallCreateStatus.Created,
                CloneCall(call));
        }
        finally
        {
            peerGate.Release();
        }
    }

    private static (int PeerType, long PeerId) ActiveCallKey(TLDto.TLGroupCallState call)
    {
        var view = call.AsGroupCallState();
        return view.Conference
            ? ((int)GroupCallPeerType.None, view.Id)
            : (view.PeerType, view.PeerId);
    }

    public async ValueTask<TLDto.TLGroupCallState?> GetCallAsync(long callId,
        CancellationToken cancellationToken = default) =>
        await GetCallInternalAsync(callId, cancellationToken);

    private static bool IsHostPeer(int peerType) =>
        peerType is (int)GroupCallPeerType.Chat or (int)GroupCallPeerType.Channel;

    public async ValueTask<TLDto.TLGroupCallState?> GetActiveCallByPeerAsync(int peerType,
        long peerId, CancellationToken cancellationToken = default)
    {
        if (!IsHostPeer(peerType))
        {
            return null;
        }
        byte[]? activePeer = await _activePeers.GetAsync(peerType, peerId);
        if (activePeer == null)
        {
            return null;
        }
        using var active = new TLDto.TLGroupCallActivePeer(activePeer, 0,
            activePeer.Length);
        return await GetCallInternalAsync(active.AsGroupCallActivePeer().CallId,
            cancellationToken);
    }

    public async ValueTask<TLDto.TLGroupCallState?> GetCallByPeerRandomIdAsync(int peerType,
        long peerId, int randomId, CancellationToken cancellationToken = default)
    {
        if (!IsHostPeer(peerType))
        {
            return null;
        }
        byte[]? bytes = await _calls.GetBySecondaryIndexAsync("by_peer_random", peerType,
            peerId, randomId);
        return bytes == null ? null : ReadCall(bytes);
    }

    public async ValueTask<IReadOnlyList<TLDto.TLGroupCallState>> GetActiveCallsAsync(
        CancellationToken cancellationToken = default)
    {
        List<TLDto.TLGroupCallState> calls = new();
        await foreach (byte[] bytes in _activePeers.IterateAsync()
                           .WithCancellation(cancellationToken))
        {
            using var active = new TLDto.TLGroupCallActivePeer(bytes, 0, bytes.Length);
            TLDto.TLGroupCallState? call = await GetCallInternalAsync(
                active.AsGroupCallActivePeer().CallId, cancellationToken);
            if (call != null)
            {
                calls.Add(call.Value);
            }
        }
        return calls;
    }

    private async ValueTask<GroupCallMutationResult> MutateCallAsync(long callId,
        Func<TLDto.TLGroupCallState, (GroupCallMutationStatus Status,
            TLDto.TLGroupCallState? Updated)> mutate,
        string operation, CancellationToken cancellationToken)
    {
        SemaphoreSlim callGate = GetGate(_callGates, callId);
        await callGate.WaitAsync(cancellationToken);
        try
        {
            using TLDto.TLGroupCallState? current =
                await GetCallInternalAsync(callId, cancellationToken);
            if (current == null)
            {
                return new GroupCallMutationResult(GroupCallMutationStatus.NotFound, null);
            }
            (GroupCallMutationStatus status, TLDto.TLGroupCallState? updated) =
                mutate(current.Value);
            if (status != GroupCallMutationStatus.Updated || updated == null)
            {
                return new GroupCallMutationResult(status, null);
            }
            TLDto.TLGroupCallState updatedCall = updated.Value;
            try
            {
                PutCall(updatedCall);
                await FlushAsync(operation);
            }
            catch
            {
                updatedCall.Dispose();
                throw;
            }
            return new GroupCallMutationResult(GroupCallMutationStatus.Updated,
                updatedCall);
        }
        finally
        {
            callGate.Release();
        }
    }

    public ValueTask<GroupCallMutationResult> TrySetJoinMutedAsync(long callId,
        bool joinMuted, CancellationToken cancellationToken = default) =>
        MutateCallAsync(callId, current =>
        {
            var view = current.AsGroupCallState();
            if (view.State != (int)GroupCallPersistenceState.Active &&
                view.State != (int)GroupCallPersistenceState.Scheduled)
            {
                return (GroupCallMutationStatus.InvalidState, null);
            }
            if (view.JoinMuted == joinMuted)
            {
                return (GroupCallMutationStatus.NoChange, null);
            }
            TLDto.TLGroupCallState updated = view.Clone()
                .JoinMuted(joinMuted)
                .Build();
            return (GroupCallMutationStatus.Updated, updated);
        }, "group call join-muted update", cancellationToken);

    public ValueTask<GroupCallMutationResult> TrySetTitleAsync(long callId, string title,
        CancellationToken cancellationToken = default) =>
        MutateCallAsync(callId, current =>
        {
            var view = current.AsGroupCallState();
            if (view.State != (int)GroupCallPersistenceState.Active &&
                view.State != (int)GroupCallPersistenceState.Scheduled)
            {
                return (GroupCallMutationStatus.InvalidState, null);
            }
            byte[] titleBytes = Encoding.UTF8.GetBytes(title);
            if (view.Flags[1] && view.Title.SequenceEqual(titleBytes))
            {
                return (GroupCallMutationStatus.NoChange, null);
            }
            TLDto.TLGroupCallState updated = view.Clone()
                .Title(titleBytes)
                .Build();
            return (GroupCallMutationStatus.Updated, updated);
        }, "group call title update", cancellationToken);

    public ValueTask<GroupCallMutationResult> TryStartScheduledAsync(long callId,
        int startedDate, CancellationToken cancellationToken = default) =>
        MutateCallAsync(callId, current =>
        {
            var view = current.AsGroupCallState();
            if (view.State != (int)GroupCallPersistenceState.Scheduled)
            {
                return (GroupCallMutationStatus.InvalidState, null);
            }
            TLDto.TLGroupCallState updated = StartScheduledCallRow(current,
                startedDate);
            return (GroupCallMutationStatus.Updated, updated);
        }, "group call scheduled start", cancellationToken);

    public async ValueTask<GroupCallViewerMutationResult>
        TrySetStartSubscriptionAsync(long callId, long userId, bool subscribed,
            CancellationToken cancellationToken = default)
    {
        SemaphoreSlim callGate = GetGate(_callGates, callId);
        await callGate.WaitAsync(cancellationToken);
        try
        {
            using TLDto.TLGroupCallState? current =
                await GetCallInternalAsync(callId, cancellationToken);
            if (current == null)
            {
                return new GroupCallViewerMutationResult(
                    GroupCallViewerMutationStatus.CallNotFound, null);
            }
            var call = current.Value.AsGroupCallState();
            if (call.State != (int)GroupCallPersistenceState.Scheduled ||
                !call.Flags[2])
            {
                return new GroupCallViewerMutationResult(
                    GroupCallViewerMutationStatus.CallNotScheduled, null);
            }

            byte[]? existingBytes = await _viewerStates.GetAsync(callId, userId);
            if (existingBytes != null)
            {
                using var existing = new TLDto.TLGroupCallViewerState(existingBytes,
                    0, existingBytes.Length);
                if (existing.AsGroupCallViewerState().ScheduleStartSubscribed ==
                    subscribed)
                {
                    return new GroupCallViewerMutationResult(
                        GroupCallViewerMutationStatus.NoChange, null);
                }
            }
            else if (!subscribed)
            {
                return new GroupCallViewerMutationResult(
                    GroupCallViewerMutationStatus.NoChange, null);
            }

            using TLDto.TLGroupCallViewerState state = TLDto.GroupCallViewerState
                .Builder()
                .CallId(callId)
                .UserId(userId)
                .ScheduleStartSubscribed(subscribed)
                .Build();
            _viewerStates.Put(state.AsSpan().ToArray(), callId, userId);
            await FlushAsync("group call start subscription");
            return new GroupCallViewerMutationResult(
                GroupCallViewerMutationStatus.Updated, CloneCall(current.Value));
        }
        finally
        {
            callGate.Release();
        }
    }

    public ValueTask<GroupCallMutationResult> TryRotateInviteGenerationAsync(long callId,
        CancellationToken cancellationToken = default) =>
        MutateCallAsync(callId, current =>
        {
            var view = current.AsGroupCallState();
            if (view.State == (int)GroupCallPersistenceState.Discarded)
            {
                return (GroupCallMutationStatus.InvalidState, null);
            }
            TLDto.TLGroupCallState updated = view.Clone()
                .InviteGeneration(view.InviteGeneration + 1)
                .Build();
            return (GroupCallMutationStatus.Updated, updated);
        }, "group call invite rotation", cancellationToken);

    public ValueTask<GroupCallMutationResult> TryAdvanceMediaEpochAsync(long callId,
        CancellationToken cancellationToken = default) =>
        MutateCallAsync(callId, current =>
        {
            var view = current.AsGroupCallState();
            if (view.State == (int)GroupCallPersistenceState.Discarded)
            {
                return (GroupCallMutationStatus.InvalidState, null);
            }
            TLDto.TLGroupCallState updated = view.Clone()
                .MediaEpoch(view.MediaEpoch + 1)
                .Build();
            return (GroupCallMutationStatus.Updated, updated);
        }, "group call media epoch advance", cancellationToken);

    public async ValueTask<GroupCallRecordingMutationResult> TryStartRecordingAsync(
        long callId, int startDate, long initiatingUserId, string title, bool video,
        bool portrait, CancellationToken cancellationToken = default)
    {
        SemaphoreSlim callGate = GetGate(_callGates, callId);
        await callGate.WaitAsync(cancellationToken);
        try
        {
            using TLDto.TLGroupCallState? current =
                await GetCallInternalAsync(callId, cancellationToken);
            if (current == null)
            {
                return new(GroupCallRecordingMutationStatus.NotFound, null);
            }

            var view = current.Value.AsGroupCallState();
            if (view.State != (int)GroupCallPersistenceState.Active)
            {
                return new(GroupCallRecordingMutationStatus.InvalidState, null);
            }
            if (view.Flags[9])
            {
                return new(GroupCallRecordingMutationStatus.NoChange,
                    CloneCall(current.Value));
            }

            int generation = view.Flags[12]
                ? checked(view.RecordingGeneration + 1)
                : 1;
            TLDto.TLGroupCallState updated = BuildRecordingCallRow(current.Value,
                start: true, startDate, initiatingUserId, title, video,
                portrait && video, generation);
            try
            {
                PutCall(updated);
                await FlushAsync("group call recording start");
            }
            catch
            {
                updated.Dispose();
                throw;
            }
            return new(GroupCallRecordingMutationStatus.Started, updated);
        }
        finally
        {
            callGate.Release();
        }
    }

    public async ValueTask<GroupCallRecordingMutationResult> TryStopRecordingAsync(
        long callId, int expectedGeneration,
        CancellationToken cancellationToken = default)
    {
        SemaphoreSlim callGate = GetGate(_callGates, callId);
        await callGate.WaitAsync(cancellationToken);
        try
        {
            using TLDto.TLGroupCallState? current =
                await GetCallInternalAsync(callId, cancellationToken);
            if (current == null)
            {
                return new(GroupCallRecordingMutationStatus.NotFound, null);
            }

            var view = current.Value.AsGroupCallState();
            if (view.State != (int)GroupCallPersistenceState.Active)
            {
                return new(GroupCallRecordingMutationStatus.InvalidState, null);
            }
            if (!view.Flags[9])
            {
                return new(GroupCallRecordingMutationStatus.NoChange,
                    CloneCall(current.Value));
            }
            if (!view.Flags[12] || view.RecordingGeneration != expectedGeneration)
            {
                return new(GroupCallRecordingMutationStatus.GenerationMismatch,
                    CloneCall(current.Value));
            }

            TLDto.TLGroupCallState updated = BuildRecordingCallRow(current.Value,
                start: false, startDate: 0, initiatingUserId: 0, title: string.Empty,
                video: false, portrait: false, expectedGeneration);
            try
            {
                PutCall(updated);
                await FlushAsync("group call recording stop");
            }
            catch
            {
                updated.Dispose();
                throw;
            }
            return new(GroupCallRecordingMutationStatus.Stopped, updated);
        }
        finally
        {
            callGate.Release();
        }
    }

    public async ValueTask<GroupCallRecoveryResult> TryMarkTransportsStaleAsync(
        long callId, CancellationToken cancellationToken = default)
    {
        SemaphoreSlim callGate = GetGate(_callGates, callId);
        await callGate.WaitAsync(cancellationToken);
        try
        {
            using TLDto.TLGroupCallState? call =
                await GetCallInternalAsync(callId, cancellationToken);
            if (call == null)
            {
                return new GroupCallRecoveryResult(
                    GroupCallRecoveryStatus.CallNotFound, 0, 0, 0);
            }

            var callView = call.Value.AsGroupCallState();
            int state = callView.State;
            int version = callView.Version;
            int mediaEpoch = callView.MediaEpoch;
            if (state != (int)GroupCallPersistenceState.Active)
            {
                return new GroupCallRecoveryResult(
                    GroupCallRecoveryStatus.CallNotActive, 0, version, mediaEpoch);
            }

            var stale = new List<TLDto.TLGroupCallParticipantState>();
            try
            {
                await foreach (byte[] bytes in _participants
                                   .IterateBySecondaryIndexAsync("by_call_order", callId)
                                   .WithCancellation(cancellationToken))
                {
                    using var participant = ReadParticipant(bytes);
                    if (!participant.AsGroupCallParticipantState().Left)
                    {
                        stale.Add(MarkParticipantLeft(participant));
                    }
                }

                if (stale.Count == 0)
                {
                    return new GroupCallRecoveryResult(
                        GroupCallRecoveryStatus.NoStaleParticipants, 0,
                        version, mediaEpoch);
                }

                var reconciledView = call.Value.AsGroupCallState();
                TLDto.TLGroupCallState updatedValue = reconciledView.Clone()
                    .ParticipantsCount(0)
                    .Version(version + stale.Count)
                    .MediaEpoch(mediaEpoch + 1)
                    .Build();
                using TLDto.TLGroupCallState updated = updatedValue;
                foreach (TLDto.TLGroupCallParticipantState participant in stale)
                {
                    long userId = participant.AsGroupCallParticipantState().UserId;
                    _participants.Delete(callId, userId);
                    PutParticipant(participant);
                }
                PutCall(updated);
                await FlushAsync("group call transport reconciliation");
                return new GroupCallRecoveryResult(GroupCallRecoveryStatus.Reconciled,
                    stale.Count, version + stale.Count, mediaEpoch + 1);
            }
            finally
            {
                foreach (TLDto.TLGroupCallParticipantState participant in stale)
                {
                    participant.Dispose();
                }
            }
        }
        finally
        {
            callGate.Release();
        }
    }

    public async ValueTask<GroupCallDiscardResult> TryDiscardCallAsync(long callId,
        int endedDate, int duration, int? expectedState = null,
        CancellationToken cancellationToken = default)
    {
        using TLDto.TLGroupCallState? peek =
            await GetCallInternalAsync(callId, cancellationToken);
        if (peek == null)
        {
            return new GroupCallDiscardResult(GroupCallDiscardStatus.NotFound, null);
        }
        long peerId = peek.Value.AsGroupCallState().PeerId;
        SemaphoreSlim peerGate = GetGate(_peerGates, peerId);
        await peerGate.WaitAsync(cancellationToken);
        SemaphoreSlim callGate = GetGate(_callGates, callId);
        await callGate.WaitAsync(cancellationToken);
        try
        {
            using TLDto.TLGroupCallState? current =
                await GetCallInternalAsync(callId, cancellationToken);
            if (current == null)
            {
                return new GroupCallDiscardResult(GroupCallDiscardStatus.NotFound, null);
            }
            bool alreadyDiscarded = current.Value.AsGroupCallState().State ==
                (int)GroupCallPersistenceState.Discarded;
            (int activePeerType, long activePeerId) = ActiveCallKey(current.Value);
            if (alreadyDiscarded)
            {
                return new GroupCallDiscardResult(GroupCallDiscardStatus.AlreadyDiscarded,
                    CloneCall(current.Value));
            }
            if (expectedState is { } expected &&
                current.Value.AsGroupCallState().State != expected)
            {
                return new GroupCallDiscardResult(GroupCallDiscardStatus.StateChanged,
                    null);
            }
            TLDto.TLGroupCallState discardedCall = DiscardCallRow(current.Value,
                endedDate, duration);
            try
            {
                PutCall(discardedCall);
                _activePeers.Delete(activePeerType, activePeerId);
                _participants.Delete(callId);
                _viewerStates.Delete(callId);
                _viewerParticipantStates.Delete(callId);
                _invites.Delete(callId);
                await FlushAsync("group call discard");
            }
            catch
            {
                discardedCall.Dispose();
                throw;
            }
            return new GroupCallDiscardResult(GroupCallDiscardStatus.Discarded,
                discardedCall);
        }
        finally
        {
            callGate.Release();
            peerGate.Release();
        }
    }

    public async ValueTask<GroupCallJoinResult> TryJoinParticipantAsync(
        TLDto.TLGroupCallParticipantState participant,
        CancellationToken cancellationToken = default)
    {
        var view = participant.AsGroupCallParticipantState();
        long callId = view.CallId;
        long userId = view.UserId;
        int source = view.Source;
        bool left = view.Left;
        if (source == 0 || left)
        {
            return new GroupCallJoinResult(GroupCallJoinStatus.InvalidSource, null, null);
        }
        SemaphoreSlim callGate = GetGate(_callGates, callId);
        await callGate.WaitAsync(cancellationToken);
        try
        {
            using TLDto.TLGroupCallState? call =
                await GetCallInternalAsync(callId, cancellationToken);
            if (call == null)
            {
                return new GroupCallJoinResult(GroupCallJoinStatus.CallNotFound, null,
                    null);
            }
            if (call.Value.AsGroupCallState().State !=
                (int)GroupCallPersistenceState.Active)
            {
                return new GroupCallJoinResult(GroupCallJoinStatus.CallNotActive, null,
                    null);
            }
            byte[]? sourceOwner = await _participants.GetBySecondaryIndexAsync("by_source",
                callId, source);
            if (sourceOwner != null)
            {
                using var owner = ReadParticipant(sourceOwner);
                var ownerView = owner.AsGroupCallParticipantState();
                bool duplicate = !ownerView.Left && ownerView.UserId != userId;
                if (duplicate)
                {
                    return new GroupCallJoinResult(GroupCallJoinStatus.DuplicateSource,
                        null, null);
                }
            }
            using TLDto.TLGroupCallParticipantState? existing =
                await GetParticipantInternalAsync(callId, userId, cancellationToken);
            bool rejoining = existing != null;
            bool wasActive = existing != null &&
                             !existing.Value.AsGroupCallParticipantState().Left;
            if (rejoining)
            {
                _participants.Delete(callId, userId);
            }
            PutParticipant(participant);
            TLDto.TLGroupCallState updatedCall = BumpCallVersion(call.Value,
                wasActive ? 0 : 1);
            try
            {
                PutCall(updatedCall);
                await FlushAsync("group call join");
            }
            catch
            {
                updatedCall.Dispose();
                throw;
            }
            return new GroupCallJoinResult(
                rejoining ? GroupCallJoinStatus.Rejoined : GroupCallJoinStatus.Joined,
                CloneParticipant(participant), updatedCall);
        }
        finally
        {
            callGate.Release();
        }
    }

    public async ValueTask<GroupCallLeaveResult> TryLeaveParticipantAsync(long callId,
        long userId, CancellationToken cancellationToken = default)
    {
        SemaphoreSlim callGate = GetGate(_callGates, callId);
        await callGate.WaitAsync(cancellationToken);
        try
        {
            using TLDto.TLGroupCallState? call =
                await GetCallInternalAsync(callId, cancellationToken);
            if (call == null)
            {
                return new GroupCallLeaveResult(GroupCallLeaveStatus.CallNotFound, null,
                    null);
            }
            using TLDto.TLGroupCallParticipantState? existing =
                await GetParticipantInternalAsync(callId, userId, cancellationToken);
            if (existing == null || existing.Value.AsGroupCallParticipantState().Left)
            {
                return new GroupCallLeaveResult(GroupCallLeaveStatus.NotJoined, null,
                    null);
            }
            TLDto.TLGroupCallParticipantState leftParticipant =
                MarkParticipantLeft(existing.Value);
            TLDto.TLGroupCallState updatedCall = BumpCallVersion(call.Value, -1);
            try
            {
                _participants.Delete(callId, userId);
                PutParticipant(leftParticipant);
                PutCall(updatedCall);
                await FlushAsync("group call leave");
            }
            catch
            {
                leftParticipant.Dispose();
                updatedCall.Dispose();
                throw;
            }
            return new GroupCallLeaveResult(GroupCallLeaveStatus.Left, leftParticipant,
                updatedCall);
        }
        finally
        {
            callGate.Release();
        }
    }

    public async ValueTask<GroupCallParticipantEditResult> TryEditParticipantAsync(
        long callId, long userId, GroupCallParticipantEditSpec edit,
        CancellationToken cancellationToken = default)
    {
        SemaphoreSlim callGate = GetGate(_callGates, callId);
        await callGate.WaitAsync(cancellationToken);
        try
        {
            using TLDto.TLGroupCallState? call =
                await GetCallInternalAsync(callId, cancellationToken);
            if (call == null)
            {
                return new GroupCallParticipantEditResult(
                    GroupCallParticipantEditStatus.CallNotFound, null, null);
            }
            using TLDto.TLGroupCallParticipantState? existing =
                await GetParticipantInternalAsync(callId, userId, cancellationToken);
            if (existing == null || existing.Value.AsGroupCallParticipantState().Left)
            {
                return new GroupCallParticipantEditResult(
                    GroupCallParticipantEditStatus.NotJoined, null, null);
            }
            TLDto.TLGroupCallParticipantState editedParticipant =
                BuildEditedParticipant(existing.Value, edit);
            if (editedParticipant.AsSpan().SequenceEqual(existing.Value.AsSpan()))
            {
                editedParticipant.Dispose();
                return new GroupCallParticipantEditResult(
                    GroupCallParticipantEditStatus.NoChange, null, null);
            }
            TLDto.TLGroupCallState updatedCall;
            try
            {
                updatedCall = BumpCallVersion(call.Value, 0);
            }
            catch
            {
                editedParticipant.Dispose();
                throw;
            }
            try
            {
                PutParticipant(editedParticipant);
                PutCall(updatedCall);
                await FlushAsync("group call participant edit");
            }
            catch
            {
                editedParticipant.Dispose();
                updatedCall.Dispose();
                throw;
            }
            return new GroupCallParticipantEditResult(
                GroupCallParticipantEditStatus.Updated, editedParticipant, updatedCall);
        }
        finally
        {
            callGate.Release();
        }
    }

    public async ValueTask<GroupCallParticipantEditResult>
        TrySetParticipantPresentationAsync(long callId, long userId,
            string? presentationEndpoint, CancellationToken cancellationToken = default)
    {
        SemaphoreSlim callGate = GetGate(_callGates, callId);
        await callGate.WaitAsync(cancellationToken);
        try
        {
            using TLDto.TLGroupCallState? call =
                await GetCallInternalAsync(callId, cancellationToken);
            if (call == null)
            {
                return new GroupCallParticipantEditResult(
                    GroupCallParticipantEditStatus.CallNotFound, null, null);
            }
            using TLDto.TLGroupCallParticipantState? existing =
                await GetParticipantInternalAsync(callId, userId, cancellationToken);
            if (existing == null || existing.Value.AsGroupCallParticipantState().Left)
            {
                return new GroupCallParticipantEditResult(
                    GroupCallParticipantEditStatus.NotJoined, null, null);
            }
            var view = existing.Value.AsGroupCallParticipantState();
            bool hasEndpoint = view.Flags[12];
            string? current = hasEndpoint
                ? Encoding.UTF8.GetString(view.PresentationEndpoint)
                : null;
            if (current == presentationEndpoint)
            {
                return new GroupCallParticipantEditResult(
                    GroupCallParticipantEditStatus.NoChange, null, null);
            }

            TLDto.TLGroupCallParticipantState edited =
                RebuildParticipant(existing.Value, presentationEndpoint);
            TLDto.TLGroupCallState updatedCall;
            try
            {
                updatedCall = BumpCallVersion(call.Value, 0);
            }
            catch
            {
                edited.Dispose();
                throw;
            }
            try
            {
                PutParticipant(edited);
                PutCall(updatedCall);
                await FlushAsync("group call presentation endpoint");
            }
            catch
            {
                edited.Dispose();
                updatedCall.Dispose();
                throw;
            }
            return new GroupCallParticipantEditResult(
                GroupCallParticipantEditStatus.Updated, edited, updatedCall);
        }
        finally
        {
            callGate.Release();
        }
    }

    public async ValueTask<int> CountActiveVideoParticipantsAsync(long callId,
        CancellationToken cancellationToken = default)
    {
        int count = 0;
        SemaphoreSlim callGate = GetGate(_callGates, callId);
        await callGate.WaitAsync(cancellationToken);
        try
        {
            await foreach (byte[] bytes in _participants
                               .IterateBySecondaryIndexAsync("by_call_order", callId)
                               .WithCancellation(cancellationToken))
            {
                using var participant = ReadParticipant(bytes);
                var view = participant.AsGroupCallParticipantState();
                if (!view.Left && view.VideoJoined)
                {
                    count++;
                }
            }
        }
        finally
        {
            callGate.Release();
        }
        return count;
    }

    public async ValueTask<bool> TryTouchParticipantActiveDateAsync(long callId,
        long userId, int activeDate, CancellationToken cancellationToken = default)
    {
        SemaphoreSlim callGate = GetGate(_callGates, callId);
        await callGate.WaitAsync(cancellationToken);
        try
        {
            using TLDto.TLGroupCallParticipantState? existing =
                await GetParticipantInternalAsync(callId, userId, cancellationToken);
            if (existing == null || existing.Value.AsGroupCallParticipantState().Left)
            {
                return false;
            }
            TLDto.TLGroupCallParticipantState touched =
                TouchParticipant(existing.Value, activeDate);
            try
            {
                PutParticipant(touched);
                await FlushAsync("group call active-date touch");
            }
            finally
            {
                touched.Dispose();
            }
            return true;
        }
        finally
        {
            callGate.Release();
        }
    }

    public async ValueTask<TLDto.TLGroupCallParticipantState?> GetParticipantAsync(
        long callId, long userId, CancellationToken cancellationToken = default)
    {
        SemaphoreSlim callGate = GetGate(_callGates, callId);
        await callGate.WaitAsync(cancellationToken);
        try
        {
            return await GetParticipantInternalAsync(callId, userId, cancellationToken);
        }
        finally
        {
            callGate.Release();
        }
    }

    public async ValueTask<TLDto.TLGroupCallParticipantState?> GetParticipantBySourceAsync(
        long callId, int source, CancellationToken cancellationToken = default)
    {
        if (source == 0)
        {
            return null;
        }
        SemaphoreSlim callGate = GetGate(_callGates, callId);
        await callGate.WaitAsync(cancellationToken);
        byte[]? bytes;
        try
        {
            bytes = await _participants.GetBySecondaryIndexAsync("by_source", callId,
                source);
        }
        finally
        {
            callGate.Release();
        }
        if (bytes == null)
        {
            return null;
        }
        var participant = ReadParticipant(bytes);
        if (participant.AsGroupCallParticipantState().Left)
        {
            participant.Dispose();
            return null;
        }
        return participant;
    }

    private static string EncodeOffset(int joinDate, long userId) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{OffsetPrefix}{joinDate}:{userId}"));

    private static bool TryDecodeOffset(string offset, out int joinDate, out long userId)
    {
        joinDate = 0;
        userId = 0;
        Span<byte> buffer = stackalloc byte[64];
        if (!Convert.TryFromBase64String(offset, buffer, out int written))
        {
            return false;
        }
        string decoded = Encoding.UTF8.GetString(buffer[..written]);
        if (!decoded.StartsWith(OffsetPrefix, StringComparison.Ordinal))
        {
            return false;
        }
        string[] parts = decoded[OffsetPrefix.Length..].Split(':');
        return parts.Length == 2 && int.TryParse(parts[0], out joinDate) &&
               long.TryParse(parts[1], out userId);
    }

    public async ValueTask<GroupCallParticipantPage> GetParticipantsPageAsync(long callId,
        string? offset, int limit, CancellationToken cancellationToken = default)
    {
        int afterJoinDate = 0;
        long afterUserId = 0;
        bool anchored = false;
        if (!string.IsNullOrEmpty(offset))
        {
            if (!TryDecodeOffset(offset, out afterJoinDate, out afterUserId))
            {
                return new GroupCallParticipantPage(
                    Array.Empty<TLDto.TLGroupCallParticipantState>(), null);
            }
            anchored = true;
        }
        List<TLDto.TLGroupCallParticipantState> page = new();
        int lastJoinDate = 0;
        long lastUserId = 0;
        bool hasMore = false;
        SemaphoreSlim callGate = GetGate(_callGates, callId);
        await callGate.WaitAsync(cancellationToken);
        try
        {
            await foreach (byte[] bytes in _participants
                               .IterateBySecondaryIndexAsync("by_call_order", callId)
                               .WithCancellation(cancellationToken))
            {
                var participant = ReadParticipant(bytes);
                var view = participant.AsGroupCallParticipantState();
                if (view.Left ||
                    (anchored && (view.JoinDate < afterJoinDate ||
                                  (view.JoinDate == afterJoinDate &&
                                   view.UserId <= afterUserId))))
                {
                    participant.Dispose();
                    continue;
                }
                if (page.Count == limit)
                {
                    participant.Dispose();
                    hasMore = true;
                    break;
                }
                lastJoinDate = view.JoinDate;
                lastUserId = view.UserId;
                page.Add(participant);
            }
        }
        finally
        {
            callGate.Release();
        }
        string? nextOffset = hasMore && page.Count > 0
            ? EncodeOffset(lastJoinDate, lastUserId)
            : null;
        return new GroupCallParticipantPage(page, nextOffset);
    }

    public async ValueTask<bool> PutViewerStateAsync(TLDto.TLGroupCallViewerState state,
        CancellationToken cancellationToken = default)
    {
        {
            var view = state.AsGroupCallViewerState();
            _viewerStates.Put(state.AsSpan().ToArray(), view.CallId, view.UserId);
        }
        await FlushAsync("group call viewer state");
        return true;
    }

    public async ValueTask<TLDto.TLGroupCallViewerState?> GetViewerStateAsync(long callId,
        long userId, CancellationToken cancellationToken = default)
    {
        byte[]? bytes = await _viewerStates.GetAsync(callId, userId);
        return bytes == null
            ? null
            : new TLDto.TLGroupCallViewerState(bytes, 0, bytes.Length);
    }

    public async ValueTask<bool> PutViewerParticipantStateAsync(
        TLDto.TLGroupCallViewerParticipantState state,
        CancellationToken cancellationToken = default)
    {
        {
            var view = state.AsGroupCallViewerParticipantState();
            _viewerParticipantStates.Put(state.AsSpan().ToArray(), view.CallId,
                view.ViewerUserId, view.TargetUserId);
        }
        await FlushAsync("group call viewer participant state");
        return true;
    }

    public async ValueTask<TLDto.TLGroupCallViewerParticipantState?>
        GetViewerParticipantStateAsync(long callId, long viewerUserId, long targetUserId,
        CancellationToken cancellationToken = default)
    {
        byte[]? bytes = await _viewerParticipantStates.GetAsync(callId, viewerUserId,
            targetUserId);
        return bytes == null
            ? null
            : new TLDto.TLGroupCallViewerParticipantState(bytes, 0, bytes.Length);
    }

    public async ValueTask<IReadOnlyList<TLDto.TLGroupCallViewerParticipantState>>
        GetViewerParticipantStatesAsync(long callId, long viewerUserId,
        CancellationToken cancellationToken = default)
    {
        List<TLDto.TLGroupCallViewerParticipantState> states = new();
        await foreach (byte[] bytes in _viewerParticipantStates
                           .IterateAsync(callId, viewerUserId)
                           .WithCancellation(cancellationToken))
        {
            states.Add(new TLDto.TLGroupCallViewerParticipantState(bytes, 0,
                bytes.Length));
        }
        return states;
    }

    public async ValueTask<bool> SaveDefaultJoinAsAsync(
        TLDto.TLGroupCallDefaultJoinAs joinAs,
        CancellationToken cancellationToken = default)
    {
        {
            var view = joinAs.AsGroupCallDefaultJoinAs();
            _defaultJoinAs.Put(joinAs.AsSpan().ToArray(), view.UserId, view.PeerType,
                view.PeerId);
        }
        await FlushAsync("group call default join-as");
        return true;
    }

    public async ValueTask<TLDto.TLGroupCallDefaultJoinAs?> GetDefaultJoinAsAsync(
        long userId, int peerType, long peerId,
        CancellationToken cancellationToken = default)
    {
        byte[]? bytes = await _defaultJoinAs.GetAsync(userId, peerType, peerId);
        return bytes == null
            ? null
            : new TLDto.TLGroupCallDefaultJoinAs(bytes, 0, bytes.Length);
    }

    public async ValueTask<bool> PutInviteAsync(TLDto.TLGroupCallInvite invite,
        CancellationToken cancellationToken = default)
    {
        var view = invite.AsGroupCallInvite();
        long callId = view.CallId;
        string hash = Encoding.UTF8.GetString(view.Hash);

        SemaphoreSlim callGate = GetGate(_callGates, callId);
        await callGate.WaitAsync(cancellationToken);
        try
        {
            using TLDto.TLGroupCallState? current =
                await GetCallInternalAsync(callId, cancellationToken);
            if (current == null || current.Value.AsGroupCallState().State ==
                (int)GroupCallPersistenceState.Discarded)
            {
                return false;
            }

            await _inviteGate.WaitAsync(cancellationToken);
            try
            {
                if (await _invites.GetBySecondaryIndexAsync("by_hash", hash) != null)
                {
                    return false;
                }

                var inviteView = invite.AsGroupCallInvite();
                int generation = current.Value.AsGroupCallState().InviteGeneration;
                byte[] bytes;
                if (inviteView.Generation == generation)
                {
                    bytes = invite.AsSpan().ToArray();
                }
                else
                {
                    using TLDto.TLGroupCallInvite normalized = inviteView.Clone()
                        .Generation(generation)
                        .Build();
                    bytes = normalized.AsSpan().ToArray();
                }
                _invites.Put(bytes, callId, hash);
                await FlushAsync("group call invite");
                return true;
            }
            finally
            {
                _inviteGate.Release();
            }
        }
        finally
        {
            callGate.Release();
        }
    }

    public async ValueTask<TLDto.TLGroupCallInvite?> GetInviteByHashAsync(string hash,
        CancellationToken cancellationToken = default)
    {
        byte[]? bytes = await _invites.GetBySecondaryIndexAsync("by_hash", hash);
        return bytes == null ? null : new TLDto.TLGroupCallInvite(bytes, 0, bytes.Length);
    }

    public async ValueTask<IReadOnlyList<TLDto.TLGroupCallInvite>> GetInvitesByCallAsync(
        long callId, CancellationToken cancellationToken = default)
    {
        List<TLDto.TLGroupCallInvite> invites = new();
        await foreach (byte[] bytes in _invites.IterateAsync(callId)
                           .WithCancellation(cancellationToken))
        {
            invites.Add(new TLDto.TLGroupCallInvite(bytes, 0, bytes.Length));
        }
        return invites;
    }

    public async ValueTask<bool> TryRevokeInviteAsync(long callId, string hash,
        CancellationToken cancellationToken = default)
    {
        SemaphoreSlim callGate = GetGate(_callGates, callId);
        await callGate.WaitAsync(cancellationToken);
        try
        {
            byte[]? bytes = await _invites.GetAsync(callId, hash);
            if (bytes == null)
            {
                return false;
            }
            using var invite = new TLDto.TLGroupCallInvite(bytes, 0, bytes.Length);
            var view = invite.AsGroupCallInvite();
            if (view.Revoked)
            {
                return true;
            }
            TLDto.TLGroupCallInvite updated = view.Clone()
                .Revoked(true)
                .Build();
            byte[] updatedBytes = updated.AsSpan().ToArray();
            updated.Dispose();
            _invites.Put(updatedBytes, callId, hash);
            await FlushAsync("group call invite revocation");
            return true;
        }
        finally
        {
            callGate.Release();
        }
    }
}
