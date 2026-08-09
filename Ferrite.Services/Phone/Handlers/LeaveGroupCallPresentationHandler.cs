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
/// phone.leaveGroupCallPresentation. Tears down only the screen-share transport
/// and clears the stored endpoint; the camera join is untouched. Idempotent — a
/// participant with no live presentation gets an empty, successful answer rather
/// than an error, because a client that already stopped sharing has nothing to
/// recover from.
/// </summary>
public sealed class LeaveGroupCallPresentationHandler : GroupCallHandlerBase
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    private readonly IGroupCallMediaPlane _media;

    public LeaveGroupCallPresentationHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log, IGroupCallMediaPlane media)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log)
    {
        _groupCallsRepository = groupCallsRepository;

        _media = media;
    }

    [TLFunction(Constructors.baseLayer_LeaveGroupCallPresentation)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (LeaveGroupCallPresentation)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);

        if (!callRead)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }

        using GroupCallResolution resolution = await ResolveCallAsync(authKeyId, callId,
            accessHash, GroupCallAccessLevel.Participate);
        if (resolution.Error != null)
        {
            return Error(resolution.Error);
        }

        GroupCallPeerAccess access = resolution.Access!;
        string? mediaId = await GetMediaIdAsync(callId, access.CurrentUserId);
        if (mediaId == null)
        {
            return Error(GroupCallErrors.GroupCallJoinMissing);
        }

        // The worker call comes first and is idempotent: a transport that is
        // already gone reports false, which is not an error here. The stale screen
        // mapping goes with it, or every viewer's next row would still carry SSRCs
        // the worker has stopped forwarding.
        await ReleasePresentationAsync(callId, mediaId);
        SourceMap.RemoveProducerPresentation(callId, mediaId);

        GroupCallParticipantEditResult stored = await _groupCallsRepository
            .TrySetParticipantPresentationAsync(callId, access.CurrentUserId,
                presentationEndpoint: null);
        if (stored.Status == GroupCallParticipantEditStatus.NoChange)
        {
            // Nothing was being shared. The camera join is untouched and the
            // caller gets an empty result rather than a spurious version step.
            Log.Debug($"📞 leaveGroupCallPresentation call:{callId} " +
                      $"user:{access.CurrentUserId} had no live presentation");
            return await BuildInvokerResultAsync(authKeyId, access.CurrentUserId,
                Array.Empty<byte[]>(), access.ChatBytes!);
        }
        if (stored.Status != GroupCallParticipantEditStatus.Updated)
        {
            stored.Participant?.Dispose();
            stored.Call?.Dispose();
            return Error(stored.Status == GroupCallParticipantEditStatus.NotJoined
                ? GroupCallErrors.GroupCallJoinMissing
                : GroupCallErrors.GroupCallInvalid);
        }

        await UnitOfWork.SaveAsync();

        using TLDto.TLGroupCallParticipantState participant = stored.Participant!.Value;
        using TLDto.TLGroupCallState updatedCall = stored.Call!.Value;
        int videoCount = await CountUnmutedVideoAsync(callId);

        GroupCallViewer viewer = await BuildViewerAsync(callId, access.CurrentUserId,
            access.CanManageCall);
        // The camera mapping this viewer already holds is reused deliberately: only
        // the presentation half of the row goes away.
        GroupCallParticipantOverlay selfOverlay = BuildOverlay(callId, mediaId, mediaId);
        var updates = new List<byte[]>(2);
        using (TLGroupCallParticipant selfRow = GroupCallBuilders.BuildParticipant(
                   participant, viewer, selfOverlay,
                   GroupCallParticipantDecoration.Versioned))
        using (TLUpdate participants = BuildParticipantsUpdate(updatedCall,
                   selfRow.AsSpan()))
        {
            updates.Add(participants.AsSpan().ToArray());
        }
        updates.Add(BuildCallUpdateBytes(updatedCall, viewer, access.Peer.Id, videoCount));

        await PushTeardownToOtherMembersAsync(updatedCall, participant, access,
            videoCount);

        Log.Debug($"📞 leaveGroupCallPresentation call:{callId} " +
                  $"user:{access.CurrentUserId} media:{mediaId}");
        return await BuildInvokerResultAsync(authKeyId, access.CurrentUserId, updates,
            access.ChatBytes!);
    }

    private static TLUpdatesResult Error(string message) =>
        (TLUpdatesResult)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    private async Task PushTeardownToOtherMembersAsync(TLDto.TLGroupCallState call,
        TLDto.TLGroupCallParticipantState participant, GroupCallPeerAccess access,
        int videoCount)
    {
        long callId = call.AsGroupCallState().Id;
        string producerMediaId = Encoding.UTF8.GetString(
            participant.AsGroupCallParticipantState().MediaId);

        await Fanout.PushGroupCallUpdatesAsync(access.Peer.Id, access.CurrentUserId,
            async memberId =>
            {
                bool canManage = await CanManageCallAsync(access.Peer.Id, memberId);
                GroupCallViewer viewer = await BuildViewerAsync(callId, memberId,
                    canManage);
                string? viewerMediaId = await GetMediaIdAsync(callId, memberId);
                GroupCallParticipantOverlay overlay = BuildOverlay(callId, viewerMediaId,
                    producerMediaId);
                using TLGroupCallParticipant row = GroupCallBuilders.BuildParticipant(
                    participant, viewer, overlay,
                    GroupCallParticipantDecoration.Versioned);
                return BuildParticipantsUpdate(call, row.AsSpan());
            });
        await PushCallUpdateToOtherMembersAsync(call, access.Peer.Id,
            access.CurrentUserId, videoCount);
    }

    private async Task ReleasePresentationAsync(long callId, string mediaId)
    {
        try
        {
            await _media.LeavePresentationAsync(callId, mediaId);
        }
        catch (GroupCallMediaException e)
        {
            Log.Warning(e, $"📞 leaveGroupCallPresentation could not tear down the " +
                           $"transport for call:{callId} media:{mediaId}");
        }
    }
}
