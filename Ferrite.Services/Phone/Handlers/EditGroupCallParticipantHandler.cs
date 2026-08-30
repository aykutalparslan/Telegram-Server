// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;
using TLUpdatesResult = Ferrite.TL.baseLayer.TLUpdates;

namespace Ferrite.Services.Phone.Handlers;

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
            return Error(GroupCallErrors.GroupCallJoinMissing);
        }

        long targetUserId = targetSelf ? access.CurrentUserId : namedUserId;
        bool isSelf = targetUserId == access.CurrentUserId;

        if (!isSelf &&
            (videoStopped != null || videoPaused != null || presentationPaused != null))
        {
            return Error(GroupCallErrors.GroupCallForbidden);
        }

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

        return await BuildNoChangeResultAsync(context);
    }

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
            return await CommitLocalEditAsync(context, mutedByYou: muted,
                localVolume: null);
        }

        return await HandleAdminMutedAsync(context, muted);
    }

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
            return await CommitCanonicalEditAsync(context,
                new GroupCallParticipantEditSpec { Muted = true, CanSelfUnmute = true },
                "admin mute (admin target)");
        }

        bool adminMuted = context.Target.Muted && !context.Target.CanSelfUnmute;
        if (muted == adminMuted)
        {
            return await BuildNoChangeResultAsync(context);
        }

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

        return await CommitCanonicalEditAsync(context,
            new GroupCallParticipantEditSpec { PresentationPaused = paused },
            "presentation pause");
    }

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

    private ValueTask<TLUpdatesResult> BuildNoChangeResultAsync(EditContext context) =>
        BuildEditResultAsync(context, Array.Empty<byte[]>());

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

    private static TLUpdatesResult Error(string message) =>
        (TLUpdatesResult)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

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
