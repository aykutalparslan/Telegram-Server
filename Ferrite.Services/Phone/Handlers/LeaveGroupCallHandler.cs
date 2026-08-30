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

public sealed class LeaveGroupCallHandler : GroupCallHandlerBase
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    private readonly IGroupCallMediaPlane _media;

    public LeaveGroupCallHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log, IGroupCallMediaPlane media)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log)
    {
        _groupCallsRepository = groupCallsRepository;

        _media = media;
    }

    [TLFunction(Constructors.baseLayer_LeaveGroupCall)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (LeaveGroupCall)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);
        int source = request.Source;

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

        string mediaId;
        using (TLDto.TLGroupCallParticipantState? existing = await _groupCallsRepository.GetParticipantAsync(callId, access.CurrentUserId))
        {
            if (existing == null)
            {
                return Error(GroupCallErrors.GroupCallJoinMissing);
            }

            var view = existing.Value.AsGroupCallParticipantState();
            if (view.Left || (source != 0 && source != view.Source))
            {
                return Error(GroupCallErrors.GroupCallJoinMissing);
            }
            mediaId = Encoding.UTF8.GetString(view.MediaId);
        }

        GroupCallLeaveResult left = await _groupCallsRepository
            .TryLeaveParticipantAsync(callId, access.CurrentUserId);
        if (left.Status != GroupCallLeaveStatus.Left)
        {
            left.Participant?.Dispose();
            left.Call?.Dispose();
            return Error(left.Status == GroupCallLeaveStatus.NotJoined
                ? GroupCallErrors.GroupCallJoinMissing
                : GroupCallErrors.GroupCallInvalid);
        }

        await UnitOfWork.SaveAsync();

        using TLDto.TLGroupCallParticipantState participant = left.Participant!.Value;
        using TLDto.TLGroupCallState updatedCall = left.Call!.Value;

        await ReleaseTransportAsync(callId, mediaId);
        SourceMap.RemoveParticipant(callId, mediaId);

        int participantsCount = updatedCall.AsGroupCallState().ParticipantsCount;
        int videoCount = await CountUnmutedVideoAsync(callId);

        GroupCallViewer viewer = await BuildViewerAsync(callId, access.CurrentUserId,
            access.CanManageCall);
        var updates = new List<byte[]>(2);
        using (TLGroupCallParticipant selfRow = GroupCallBuilders.BuildParticipant(
                   participant, viewer, GroupCallParticipantOverlay.None,
                   GroupCallParticipantDecoration.Versioned))
        using (TLUpdate participants = BuildParticipantsUpdate(updatedCall,
                   selfRow.AsSpan()))
        {
            updates.Add(participants.AsSpan().ToArray());
        }

        Log.Debug($"📞 leaveGroupCall call:{callId} user:{access.CurrentUserId} " +
                  $"source:{source} media:{mediaId} remaining:{participantsCount} " +
                  $"conference:{access.IsConference}");

        if (access.IsConference)
        {
            updates.Add(BuildConferenceCallUpdateBytes(updatedCall, viewer, videoCount));
            await PushConferenceLeaveAsync(updatedCall, participant, access, videoCount);
            return await BuildConferenceResultAsync(authKeyId, access.CurrentUserId,
                updates);
        }

        byte[] chatBytes = ChatLink.SetCallFlags(access.Kind, access.ChatBytes!,
            callActive: true, callNotEmpty: participantsCount > 0);
        await UnitOfWork.SaveAsync();
        updates.Add(BuildCallUpdateBytes(updatedCall, viewer, access.Peer.Id, videoCount));
        await PushLeaveToOtherMembersAsync(updatedCall, participant, access, videoCount);
        return await BuildInvokerResultAsync(authKeyId, access.CurrentUserId, updates,
            chatBytes);
    }

    private async Task PushConferenceLeaveAsync(TLDto.TLGroupCallState call,
        TLDto.TLGroupCallParticipantState participant, GroupCallPeerAccess access,
        int videoCount)
    {
        long callId = call.AsGroupCallState().Id;
        long creatorUserId = access.Peer.Id;
        List<long> members = await GetConferenceMemberIdsAsync(callId,
            access.CurrentUserId);

        await Fanout.PushGroupCallUpdatesToAsync(members, async memberId =>
        {
            GroupCallViewer viewer = await BuildViewerAsync(callId, memberId,
                memberId == creatorUserId);
            using TLGroupCallParticipant row = GroupCallBuilders.BuildParticipant(
                participant, viewer, GroupCallParticipantOverlay.None,
                GroupCallParticipantDecoration.Versioned);
            return BuildParticipantsUpdate(call, row.AsSpan());
        });

        await Fanout.PushGroupCallUpdatesToAsync(members, async memberId =>
        {
            GroupCallViewer viewer = await BuildViewerAsync(callId, memberId,
                memberId == creatorUserId);
            using Ferrite.TL.baseLayer.TLGroupCall built = GroupCallBuilders
                .BuildCall(call, viewer, VideoOptions, videoCount);
            return UpdateGroupCall.Builder().Call(built.AsSpan()).Build();
        });
    }

    private static TLUpdatesResult Error(string message) =>
        (TLUpdatesResult)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    private async Task PushLeaveToOtherMembersAsync(TLDto.TLGroupCallState call,
        TLDto.TLGroupCallParticipantState participant, GroupCallPeerAccess access,
        int videoCount)
    {
        long callId = call.AsGroupCallState().Id;
        await Fanout.PushGroupCallUpdatesAsync(access.Peer.Id, access.CurrentUserId,
            async memberId =>
            {
                bool canManage = await CanManageCallAsync(access.Peer.Id, memberId);
                GroupCallViewer viewer = await BuildViewerAsync(callId, memberId,
                    canManage);
                using TLGroupCallParticipant row = GroupCallBuilders.BuildParticipant(
                    participant, viewer, GroupCallParticipantOverlay.None,
                    GroupCallParticipantDecoration.Versioned);
                return BuildParticipantsUpdate(call, row.AsSpan());
            });
        await PushCallUpdateToOtherMembersAsync(call, access.Peer.Id,
            access.CurrentUserId, videoCount);
    }

    private async Task ReleaseTransportAsync(long callId, string mediaId)
    {
        try
        {
            await _media.LeaveAsync(callId, mediaId);
        }
        catch (GroupCallMediaException e)
        {
            Log.Warning(e, $"📞 leaveGroupCall could not tear down the transport for " +
                           $"call:{callId} media:{mediaId}; the row is already left");
        }
    }
}
