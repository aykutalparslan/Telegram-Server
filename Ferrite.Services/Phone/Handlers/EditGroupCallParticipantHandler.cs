// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;
using TLUpdatesResult = Ferrite.TL.baseLayer.TLUpdates;

namespace Ferrite.Services.Phone.Handlers;

/// <summary>
/// phone.editGroupCallParticipant. One request edits exactly one aspect of one
/// participant, in three distinct scopes that must never be flattened into each
/// other:
///
/// - SELF: own mute/unmute, raise hand, and the video flags (video_stopped /
///   video_paused / presentation_paused, which are self-only by contract).
/// - ADMIN (manage-call right): global mute/unmute, admin-set volume, lowering
///   another participant's hand. These mutate the canonical row, consume exactly
///   one call version, and fan a versioned row to every member.
/// - LOCAL (any participant, no manage right): muting another participant or
///   setting their volume FOR THIS VIEWER ONLY. These write the viewer-local
///   row, consume NO version, and answer the invoker with a non-versioned row at
///   the call's current version — pinned TDLib applies a non-versioned row whose
///   version is not ahead (pending_mute_updates,
///   GroupCallManager.cpp:2504-2543), and no other member hears about it.
///
/// The wire mute states follow pinned TDLib's decoding exactly
/// (GroupCallParticipant.cpp:25-26): unmuted is (muted:false, can_self_unmute:
/// false), muted-by-themselves is (true, true), muted-by-admin is (true, false).
/// Global admin mute/unmute moves the worker's edge mute BEFORE the state commit
/// and compensates when the commit loses, so the canonical state is never ahead
/// of what the worker enforces.
/// </summary>
public sealed class EditGroupCallParticipantHandler : GroupCallHandlerBase
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    private const int MinVolume = 1;
    private const int MaxVolume = 20000;

    private readonly IGroupCallMediaPlane _media;

    public EditGroupCallParticipantHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log, IGroupCallMediaPlane media)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log)
    {
        _groupCallsRepository = groupCallsRepository;

        _media = media;
    }

    [TLFunction(Constructors.baseLayer_EditGroupCallParticipant)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        bool? muted = null;
        int? volume = null;
        bool? raiseHand = null;
        bool? videoStopped = null;
        bool? videoPaused = null;
        bool? presentationPaused = null;
        // The request view and its nested peer views are ref structs, so every
        // field is read here before the first await.
        var request = (EditGroupCallParticipant)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);
        bool targetRead = TryReadTargetPeer(request.Get_ParticipantView(), out bool targetSelf,
            out long namedUserId);
        if (request.Flags[0])
        {
            muted = request.Muted;
        }
        if (request.Flags[1])
        {
            volume = request.Volume;
        }
        if (request.Flags[2])
        {
            raiseHand = request.RaiseHand;
        }
        if (request.Flags[3])
        {
            videoStopped = request.VideoStopped;
        }
        if (request.Flags[4])
        {
            videoPaused = request.VideoPaused;
        }
        if (request.Flags[5])
        {
            presentationPaused = request.PresentationPaused;
        }

        if (!callRead)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }
        if (!targetRead)
        {
            return Error(GroupCallErrors.ParticipantIdInvalid);
        }

        using GroupCallResolution resolution = await ResolveCallAsync(authKeyId, callId,
            accessHash, GroupCallAccessLevel.Participate);
        if (resolution.Error != null)
        {
            return Error(resolution.Error);
        }

        GroupCallPeerAccess access = resolution.Access!;
        TLDto.TLGroupCallState call = resolution.Call!.Value;
        if (call.AsGroupCallState().State != (int)GroupCallPersistenceState.Active)
        {
            // Pinned TDLib names this exact precondition GROUPCALL_JOIN_MISSING
            // for every participant toggle (GroupCallManager.cpp:4797 etc.).
            return Error(GroupCallErrors.GroupCallJoinMissing);
        }

        long targetUserId = targetSelf ? access.CurrentUserId : namedUserId;
        bool isSelf = targetUserId == access.CurrentUserId;

        // The three video flags are SELF-ONLY: video state belongs to the sending
        // participant, so a request naming another peer with any of them set is
        // rejected outright, never silently applied to someone else's stream.
        if (!isSelf &&
            (videoStopped != null || videoPaused != null || presentationPaused != null))
        {
            return Error(GroupCallErrors.GroupCallForbidden);
        }

        // The invoker must itself hold an active join before it may edit anyone.
        string? invokerMediaId = await GetMediaIdAsync(callId, access.CurrentUserId);
        if (invokerMediaId == null)
        {
            return Error(GroupCallErrors.GroupCallJoinMissing);
        }

        TargetState target;
        using (TLDto.TLGroupCallParticipantState? row = await _groupCallsRepository.GetParticipantAsync(callId, targetUserId))
        {
            if (row == null || row.Value.AsGroupCallParticipantState().Left)
            {
                return Error(GroupCallErrors.ParticipantIdInvalid);
            }
            target = ReadTargetState(row.Value);
        }

        var context = new EditContext(authKeyId, callId, access, call, isSelf,
            targetUserId, invokerMediaId, target);

        // At most one branch is served per request, chosen with the pinned
        // client's own encoder priority (EditGroupCallParticipantQuery::send):
        // raise_hand, then volume, then muted, then the video flags. TDLib never
        // sends more than one, so a real request is never truncated by this.
        if (raiseHand is { } hand)
        {
            return await HandleRaiseHandAsync(context, hand);
        }
        if (volume is { } level)
        {
            return await HandleVolumeAsync(context, level);
        }
        if (muted is { } mute)
        {
            return await HandleMutedAsync(context, mute);
        }
        if (videoStopped is { } stopped)
        {
            return await HandleVideoStoppedAsync(context, stopped);
        }
        if (videoPaused is { } paused)
        {
            return await HandleVideoPausedAsync(context, paused);
        }
        if (presentationPaused is { } screenPaused)
        {
            return await HandlePresentationPausedAsync(context, screenPaused);
        }

        // A request with no branch flag changes nothing.
        return await BuildNoChangeResultAsync(context);
    }

    // ------------------------------------------------------------------ branches

    private async ValueTask<TLUpdatesResult> HandleRaiseHandAsync(EditContext context,
        bool raise)
    {
        if (raise && !context.IsSelf)
        {
            return Error(GroupCallErrors.GroupCallForbidden);
        }
        if (!raise && !context.IsSelf && !context.Access.CanManageCall)
        {
            return Error(GroupCallErrors.ChatAdminRequired);
        }
        if (raise == context.Target.HandRaised)
        {
            return await BuildNoChangeResultAsync(context);
        }

        // Later raises get strictly larger ratings so clients sort the queue by
        // recency; the exact scale is not part of the wire contract.
        GroupCallParticipantEditSpec spec = raise
            ? new GroupCallParticipantEditSpec
            {
                RaiseHandRating = Time.GetUnixTimeInSeconds()
            }
            : new GroupCallParticipantEditSpec { ClearRaiseHand = true };
        return await CommitCanonicalEditAsync(context, spec, "raise_hand");
    }

    private async ValueTask<TLUpdatesResult> HandleVolumeAsync(EditContext context,
        int volume)
    {
        if (volume is < MinVolume or > MaxVolume || context.IsSelf)
        {
            return Error(GroupCallErrors.VolumeInvalid);
        }

        if (context.Access.CanManageCall)
        {
            if (context.Target.HasAdminVolume && context.Target.Volume == volume)
            {
                return await BuildNoChangeResultAsync(context);
            }
            return await CommitCanonicalEditAsync(context,
                new GroupCallParticipantEditSpec { Volume = volume }, "admin volume");
        }

        return await CommitLocalEditAsync(context, mutedByYou: null, localVolume: volume);
    }

    private async ValueTask<TLUpdatesResult> HandleMutedAsync(EditContext context,
        bool muted)
    {
        if (context.IsSelf)
        {
            if (muted == context.Target.Muted)
            {
                return await BuildNoChangeResultAsync(context);
            }
            if (!muted && !context.Target.CanSelfUnmute)
            {
                // Muted by an admin: only an admin unmute can lift it.
                return Error(GroupCallErrors.GroupCallForbidden);
            }
            return await CommitCanonicalEditAsync(context,
                new GroupCallParticipantEditSpec
                {
                    Muted = muted,
                    CanSelfUnmute = muted
                }, muted ? "self mute" : "self unmute");
        }

        if (!context.Access.CanManageCall)
        {
            // A regular participant muting someone else is a viewer-local mute,
            // never a canonical change.
            return await CommitLocalEditAsync(context, mutedByYou: muted,
                localVolume: null);
        }

        return await HandleAdminMutedAsync(context, muted);
    }

    /// <summary>
    /// The admin transitions mirror pinned TDLib's capability matrix
    /// (GroupCallParticipant::update_can_be_muted): a non-admin target is muted to
    /// (true,false) and unmuted to (true,true) — "allowed to speak", the target
    /// still lifts its own mute; an admin target can only be muted to (true,true)
    /// and never unmuted by someone else.
    /// </summary>
    private async ValueTask<TLUpdatesResult> HandleAdminMutedAsync(EditContext context,
        bool muted)
    {
        bool targetManages = await CanManageCallAsync(context.Access.Peer.Id,
            context.TargetUserId);
        if (targetManages)
        {
            if (!muted)
            {
                return context.Target.Muted
                    ? Error(GroupCallErrors.GroupCallForbidden)
                    : await BuildNoChangeResultAsync(context);
            }
            if (context.Target.Muted)
            {
                return await BuildNoChangeResultAsync(context);
            }
            // No edge mute for an admin target: it may unmute itself at any time,
            // so the worker must keep forwarding the moment it does.
            return await CommitCanonicalEditAsync(context,
                new GroupCallParticipantEditSpec { Muted = true, CanSelfUnmute = true },
                "admin mute (admin target)");
        }

        bool adminMuted = context.Target.Muted && !context.Target.CanSelfUnmute;
        if (muted == adminMuted)
        {
            return await BuildNoChangeResultAsync(context);
        }

        // Edge enforcement precedes the commit: if the worker cannot enforce the
        // mute, the canonical state must not advertise it.
        try
        {
            await _media.SetIngressMuteAsync(context.CallId, context.Target.MediaId,
                muted);
        }
        catch (GroupCallMediaException e)
        {
            Log.Warning(e, $"📞 editGroupCallParticipant edge mute failed for " +
                           $"call:{context.CallId} target:{context.TargetUserId} " +
                           $"kind:{e.Kind}");
            return Error(GroupCallErrors.MediaUnavailable);
        }

        GroupCallParticipantEditSpec spec = muted
            ? new GroupCallParticipantEditSpec { Muted = true, CanSelfUnmute = false }
            : new GroupCallParticipantEditSpec { Muted = true, CanSelfUnmute = true };
        return await CommitCanonicalEditAsync(context, spec,
            muted ? "admin mute" : "admin unmute",
            compensate: () => RestoreEdgeMuteAsync(context, !muted));
    }

    private async ValueTask<TLUpdatesResult> HandleVideoStoppedAsync(EditContext context,
        bool stopped)
    {
        if (stopped)
        {
            if (!context.Target.VideoJoined && context.Target.VideoStopped)
            {
                return await BuildNoChangeResultAsync(context);
            }
            // The stored endpoint is deliberately KEPT: it names the still-live
            // camera transport the worker allocated at join, and it is the durable
            // marker that lets video_stopped:false turn the camera back on without
            // a rejoin.
            return await CommitCanonicalEditAsync(context,
                new GroupCallParticipantEditSpec
                {
                    VideoStopped = true,
                    VideoJoined = false
                }, "video stop", videoStateChanged: context.Target.VideoJoined);
        }

        bool canJoinVideo = context.Target.HasVideoEndpoint;
        if (!context.Target.VideoStopped && context.Target.VideoJoined == canJoinVideo)
        {
            return await BuildNoChangeResultAsync(context);
        }
        return await CommitCanonicalEditAsync(context,
            new GroupCallParticipantEditSpec
            {
                VideoStopped = false,
                VideoJoined = canJoinVideo
            }, "video start",
            videoStateChanged: canJoinVideo != context.Target.VideoJoined);
    }

    private async ValueTask<TLUpdatesResult> HandleVideoPausedAsync(EditContext context,
        bool paused)
    {
        if (!context.Target.VideoJoined)
        {
            return Error(GroupCallErrors.GroupCallForbidden);
        }
        if (paused == context.Target.VideoPaused)
        {
            return await BuildNoChangeResultAsync(context);
        }

        // The worker stops/resumes forwarding before the state commit and is
        // rolled back when the commit loses, matching the global-mute rule.
        try
        {
            await _media.SetVideoPausedAsync(context.CallId, context.Target.MediaId,
                paused);
        }
        catch (GroupCallMediaException e)
        {
            Log.Warning(e, $"📞 editGroupCallParticipant video pause failed for " +
                           $"call:{context.CallId} user:{context.TargetUserId} " +
                           $"kind:{e.Kind}");
            return Error(GroupCallErrors.MediaUnavailable);
        }

        return await CommitCanonicalEditAsync(context,
            new GroupCallParticipantEditSpec { VideoPaused = paused }, "video pause",
            compensate: () => RestoreVideoPauseAsync(context, !paused));
    }

    private async ValueTask<TLUpdatesResult> HandlePresentationPausedAsync(
        EditContext context, bool paused)
    {
        if (!context.Target.HasPresentation)
        {
            return Error(GroupCallErrors.GroupCallForbidden);
        }
        if (paused == context.Target.PresentationPaused)
        {
            return await BuildNoChangeResultAsync(context);
        }

        // State-only: the media plane exposes no pause operation for the screen
        // half (SetVideoPausedAsync addresses the camera transport), and the
        // sharer's client stops sending on its own; viewers need the row.
        return await CommitCanonicalEditAsync(context,
            new GroupCallParticipantEditSpec { PresentationPaused = paused },
            "presentation pause");
    }

    // ------------------------------------------------------------ commit helpers

    /// <summary>
    /// One canonical edit: exactly one version increment, a versioned row fanned
    /// to every member, and — when the edit changed whether the participant sends
    /// video — a refreshed viewer-correct call row on both channels.
    /// </summary>
    private async ValueTask<TLUpdatesResult> CommitCanonicalEditAsync(EditContext context,
        GroupCallParticipantEditSpec spec, string operation,
        bool videoStateChanged = false, Func<Task>? compensate = null)
    {
        GroupCallParticipantEditResult edited = await _groupCallsRepository
            .TryEditParticipantAsync(context.CallId, context.TargetUserId, spec);
        if (edited.Status == GroupCallParticipantEditStatus.NoChange)
        {
            return await BuildNoChangeResultAsync(context);
        }
        if (edited.Status != GroupCallParticipantEditStatus.Updated)
        {
            edited.Participant?.Dispose();
            edited.Call?.Dispose();
            // The repository refused, so nothing durable changed; roll the worker
            // back to the state the stored row still advertises.
            if (compensate != null)
            {
                await compensate();
            }
            return Error(edited.Status == GroupCallParticipantEditStatus.NotJoined
                ? GroupCallErrors.ParticipantIdInvalid
                : GroupCallErrors.GroupCallInvalid);
        }

        await UnitOfWork.SaveAsync();

        using TLDto.TLGroupCallParticipantState participant = edited.Participant!.Value;
        using TLDto.TLGroupCallState updatedCall = edited.Call!.Value;

        GroupCallPeerAccess access = context.Access;
        int videoCount = await CountUnmutedVideoAsync(context.CallId);
        GroupCallViewer viewer = await BuildViewerAsync(context.CallId,
            access.CurrentUserId, access.CanManageCall);
        GroupCallParticipantOverlay overlay = context.IsSelf
            ? BuildOverlay(context.CallId, context.InvokerMediaId, context.Target.MediaId)
            : await BuildMemberOverlayAsync(context.CallId, access.CurrentUserId,
                context.InvokerMediaId, context.TargetUserId, context.Target.MediaId);

        var updates = new List<byte[]>(2);
        using (TLGroupCallParticipant row = GroupCallBuilders.BuildParticipant(
                   participant, viewer, overlay, GroupCallParticipantDecoration.Versioned))
        using (TLUpdate participants = BuildParticipantsUpdate(updatedCall, row.AsSpan()))
        {
            updates.Add(participants.AsSpan().ToArray());
        }
        if (videoStateChanged)
        {
            updates.Add(BuildCallUpdateBytes(updatedCall, viewer, access.Peer.Id,
                videoCount));
        }

        await PushEditToOtherMembersAsync(updatedCall, participant, context,
            videoStateChanged, videoCount);

        Log.Debug($"📞 editGroupCallParticipant {operation} call:{context.CallId} " +
                  $"by:{access.CurrentUserId} target:{context.TargetUserId} " +
                  $"version:{updatedCall.AsGroupCallState().Version}");
        return await BuildEditResultAsync(context, updates);
    }

    /// <summary>
    /// A viewer-local edit: the viewer-local row is replaced, no call version is
    /// consumed, and only the invoker receives the re-rendered row — non-versioned
    /// at the call's CURRENT version, which pinned TDLib applies as a mute update
    /// rather than a versioned step.
    /// </summary>
    private async ValueTask<TLUpdatesResult> CommitLocalEditAsync(EditContext context,
        bool? mutedByYou, int? localVolume)
    {
        (bool storedMuted, int? storedVolume) = await ReadLocalStateAsync(context);
        bool newMuted = mutedByYou ?? storedMuted;
        int? newVolume = localVolume ?? storedVolume;

        var builder = TLDto.GroupCallViewerParticipantState.Builder()
            .CallId(context.CallId)
            .ViewerUserId(context.Access.CurrentUserId)
            .TargetUserId(context.TargetUserId);
        if (newMuted)
        {
            builder = builder.MutedByYou(true);
        }
        if (newVolume is { } volume)
        {
            builder = builder.Volume(volume);
        }
        using (TLDto.TLGroupCallViewerParticipantState state = builder.Build())
        {
            await _groupCallsRepository.PutViewerParticipantStateAsync(state);
        }
        await UnitOfWork.SaveAsync();

        GroupCallViewer viewer = await BuildViewerAsync(context.CallId,
            context.Access.CurrentUserId, context.Access.CanManageCall);
        GroupCallParticipantOverlay overlay = BuildOverlay(context.CallId,
            context.InvokerMediaId, context.Target.MediaId, newMuted, newVolume);

        var updates = new List<byte[]>(1);
        using (TLDto.TLGroupCallParticipantState? row = await _groupCallsRepository.GetParticipantAsync(context.CallId,
                       context.TargetUserId))
        {
            if (row != null)
            {
                using TLGroupCallParticipant built = GroupCallBuilders.BuildParticipant(
                    row.Value, viewer, overlay);
                using TLUpdate participants = BuildParticipantsUpdate(context.Call,
                    built.AsSpan());
                updates.Add(participants.AsSpan().ToArray());
            }
        }

        Log.Debug($"📞 editGroupCallParticipant local call:{context.CallId} " +
                  $"viewer:{context.Access.CurrentUserId} target:{context.TargetUserId} " +
                  $"muted_by_you:{newMuted} volume:{newVolume?.ToString() ?? "-"}");
        return await BuildEditResultAsync(context, updates);
    }

    /// <summary>
    /// An edit that changes nothing still succeeds: pinned TDLib does not treat
    /// GROUPCALL_NOT_MODIFIED as success for this method, so an error here would
    /// fail app-level toggles that merely retried.
    /// </summary>
    private ValueTask<TLUpdatesResult> BuildNoChangeResultAsync(EditContext context) =>
        BuildEditResultAsync(context, Array.Empty<byte[]>());

    /// <summary>
    /// Every edit answer travels sequenced on the invoker's own key and hydrates
    /// both the invoker and the edited participant.
    /// </summary>
    private async ValueTask<TLUpdatesResult> BuildEditResultAsync(EditContext context,
        IReadOnlyCollection<byte[]> updates) =>
        await BuildInvokerResultAsync(context.AuthKeyId, context.Access.CurrentUserId,
            updates, context.Access.ChatBytes!, new[] { context.TargetUserId });

    private async ValueTask<(bool Muted, int? Volume)> ReadLocalStateAsync(
        EditContext context)
    {
        using TLDto.TLGroupCallViewerParticipantState? local = await _groupCallsRepository.GetViewerParticipantStateAsync(context.CallId,
                context.Access.CurrentUserId, context.TargetUserId);
        if (local == null)
        {
            return (false, null);
        }

        var view = local.Value.AsGroupCallViewerParticipantState();
        return (view.MutedByYou, view.Flags[1] ? view.Volume : null);
    }

    private async Task PushEditToOtherMembersAsync(TLDto.TLGroupCallState call,
        TLDto.TLGroupCallParticipantState participant, EditContext context,
        bool videoStateChanged, int videoCount)
    {
        long callId = context.CallId;
        GroupCallPeerAccess access = context.Access;
        string producerMediaId = Encoding.UTF8.GetString(
            participant.AsGroupCallParticipantState().MediaId);

        await Fanout.PushGroupCallUpdatesAsync(access.Peer.Id, access.CurrentUserId,
            async memberId =>
            {
                bool canManage = await CanManageCallAsync(access.Peer.Id, memberId);
                GroupCallViewer viewer = await BuildViewerAsync(callId, memberId,
                    canManage);
                string? viewerMediaId = await GetMediaIdAsync(callId, memberId);
                GroupCallParticipantOverlay overlay = await BuildMemberOverlayAsync(
                    callId, memberId, viewerMediaId, context.TargetUserId,
                    producerMediaId);
                using TLGroupCallParticipant row = GroupCallBuilders.BuildParticipant(
                    participant, viewer, overlay,
                    GroupCallParticipantDecoration.Versioned);
                return BuildParticipantsUpdate(call, row.AsSpan());
            });
        if (videoStateChanged)
        {
            await PushCallUpdateToOtherMembersAsync(call, access.Peer.Id,
                access.CurrentUserId, videoCount);
        }
    }

    /// <summary>
    /// Rolls the worker's edge mute back after a lost commit. Best-effort: the
    /// canonical state did not change, and a worker that is now unreachable will
    /// be reconciled by the next successful edit.
    /// </summary>
    private async Task RestoreEdgeMuteAsync(EditContext context, bool muted)
    {
        try
        {
            await _media.SetIngressMuteAsync(context.CallId, context.Target.MediaId,
                muted);
        }
        catch (GroupCallMediaException e)
        {
            Log.Warning(e, $"📞 editGroupCallParticipant could not roll back the edge " +
                           $"mute for call:{context.CallId} " +
                           $"target:{context.TargetUserId}");
        }
    }

    private async Task RestoreVideoPauseAsync(EditContext context, bool paused)
    {
        try
        {
            await _media.SetVideoPausedAsync(context.CallId, context.Target.MediaId,
                paused);
        }
        catch (GroupCallMediaException e)
        {
            Log.Warning(e, $"📞 editGroupCallParticipant could not roll back the video " +
                           $"pause for call:{context.CallId} " +
                           $"target:{context.TargetUserId}");
        }
    }

    // ---------------------------------------------------------------- plumbing

    private static TLUpdatesResult Error(string message) =>
        (TLUpdatesResult)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    /// <summary>
    /// The edited participant is a user: inputPeerSelf or inputPeerUser. Join-as
    /// peers are supported; every other shape cannot name a served participant.
    /// </summary>
    private static bool TryReadTargetPeer(InputPeerView peer, out bool self,
        out long userId)
    {
        if (peer.Is(out InputPeerSelf _))
        {
            self = true;
            userId = 0;
            return true;
        }
        if (peer.Is(out InputPeerUser user) && user.UserId > 0)
        {
            self = false;
            userId = user.UserId;
            return true;
        }

        self = false;
        userId = 0;
        return false;
    }

    /// <summary>
    /// Everything the branches need from the target's stored row, copied out
    /// because the row view is a ref struct that cannot cross an await.
    /// </summary>
    private readonly record struct TargetState(bool Muted, bool CanSelfUnmute,
        bool HandRaised, bool HasAdminVolume, int Volume, bool VideoJoined,
        bool VideoStopped, bool VideoPaused, bool HasVideoEndpoint,
        bool HasPresentation, bool PresentationPaused, string MediaId);

    private static TargetState ReadTargetState(TLDto.TLGroupCallParticipantState row)
    {
        var view = row.AsGroupCallParticipantState();
        return new TargetState(view.Muted, view.CanSelfUnmute, view.Flags[5],
            view.Flags[4], view.Flags[4] ? view.Volume : 0, view.VideoJoined,
            view.VideoStopped, view.VideoPaused, view.Flags[11], view.Flags[12],
            view.PresentationPaused, Encoding.UTF8.GetString(view.MediaId));
    }

    private sealed record EditContext(long AuthKeyId, long CallId,
        GroupCallPeerAccess Access, TLDto.TLGroupCallState Call, bool IsSelf,
        long TargetUserId, string InvokerMediaId, TargetState Target);
}
