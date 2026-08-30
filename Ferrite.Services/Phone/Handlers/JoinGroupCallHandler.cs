// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.Services.Calls.E2E;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;
using TLUpdatesResult = Ferrite.TL.baseLayer.TLUpdates;

namespace Ferrite.Services.Phone.Handlers;

public sealed class JoinGroupCallHandler : GroupCallHandlerBase
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    private readonly IGroupCallMediaPlane _media;
    private readonly ConferenceJoinOperation _conferenceJoin;

    public JoinGroupCallHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log, IGroupCallMediaPlane media,
        ConferenceJoinOperation conferenceJoin)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log)
    {
        _groupCallsRepository = groupCallsRepository;

        _media = media;
        _conferenceJoin = conferenceJoin;
    }

    [TLFunction(Constructors.baseLayer_JoinGroupCall)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        ConferenceCallRef conferenceRef;
        var request = (JoinGroupCall)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);
        bool conference = request.Flags[3];
        if (conference)
        {
            callRead = ConferenceCallHandlerBase.TryReadConferenceRef(
                request.Get_CallView(), out conferenceRef);
        }
        else
        {
            conferenceRef = default;
        }
        bool requestedMuted = request.Muted;
        bool videoStopped = request.VideoStopped;
        string? inviteHash = request.Flags[1]
            ? Encoding.UTF8.GetString(request.InviteHash)
            : null;
        bool joinAsRead = TryReadSelfJoinAs(request.Get_JoinAsView(), out long joinAsUserId);
        byte[] paramsJson = ReadParamsJson(request.Get_ParamsPropertyView());
        byte[] publicKey = conference ? request.PublicKey.ToArray() : Array.Empty<byte>();
        byte[] block = conference ? request.Block.ToArray() : Array.Empty<byte>();

        if (!callRead)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }
        if (conference)
        {
            return await _conferenceJoin.JoinAsync(authKeyId, conferenceRef,
                publicKey, block, paramsJson, requestedMuted, videoStopped);
        }
        if (!joinAsRead)
        {
            return Error(GroupCallErrors.JoinAsPeerInvalid);
        }

        using GroupCallResolution resolution = await ResolveCallAsync(authKeyId, callId,
            accessHash, GroupCallAccessLevel.Participate);
        if (resolution.Error != null)
        {
            return Error(resolution.Error);
        }

        GroupCallPeerAccess access = resolution.Access!;
        TLDto.TLGroupCallState call = resolution.Call!.Value;
        if (joinAsUserId != 0 && joinAsUserId != access.CurrentUserId)
        {
            return Error(GroupCallErrors.JoinAsPeerInvalid);
        }

        var view = call.AsGroupCallState();
        if (view.State != (int)GroupCallPersistenceState.Active)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }
        bool callJoinMuted = view.JoinMuted;
        int callInviteGeneration = view.InviteGeneration;
        bool rtmpStream = view.RtmpStream;

        GroupCallJoinPayload payload;
        try
        {
            payload = GroupCallJoinPayloadCodec.ParseJoinPayload(paramsJson);
        }
        catch (GroupCallDataJsonException e)
        {
            Log.Debug($"📞 joinGroupCall rejected the payload for call:{callId} " +
                      $"user:{access.CurrentUserId}: {e.Message}");
            return Error(GroupCallErrors.DataJsonInvalid);
        }

        bool inviteSelfUnmute = false;
        if (inviteHash != null)
        {
            InviteDecision decision = await ResolveInviteAsync(callId,
                callInviteGeneration, inviteHash);
            if (!decision.Valid)
            {
                return Error(GroupCallErrors.InviteHashExpired);
            }
            inviteSelfUnmute = decision.CanSelfUnmute;
        }

        if (rtmpStream)
        {
            try
            {
                await _media.CreateRoomAsync(callId);
            }
            catch (GroupCallMediaException e)
            {
                Log.Warning(e, $"📞 joinGroupCall could not allocate the lazy " +
                               $"RTMP call room for call:{callId}");
                return Error(GroupCallJoinRows.TranslateMediaFailure(e.Kind));
            }
        }

        string mediaId;
        bool rejoining;
        using (TLDto.TLGroupCallParticipantState? existing = await _groupCallsRepository.GetParticipantAsync(callId, access.CurrentUserId))
        {
            rejoining = existing != null;
            mediaId = existing != null
                ? Encoding.UTF8.GetString(
                    existing.Value.AsGroupCallParticipantState().MediaId)
                : Guid.NewGuid().ToString("N");
        }

        if (rejoining)
        {
            await GroupCallJoinRows.ReleaseTransportAsync(_media, Log, callId, mediaId,
                "stale rejoin transport");
        }

        GroupCallMediaJoinResult joined;
        try
        {
            joined = await _media.JoinAsync(new GroupCallMediaJoinRequest(callId, mediaId,
                payload));
        }
        catch (GroupCallMediaException e)
        {
            Log.Warning(e, $"📞 joinGroupCall media join failed for call:{callId} " +
                           $"user:{access.CurrentUserId} kind:{e.Kind}");
            return Error(GroupCallJoinRows.TranslateMediaFailure(e.Kind));
        }

        int now = Now();
        bool muted = requestedMuted || (callJoinMuted && !access.CanManageCall);
        bool canSelfUnmute = muted &&
                             (access.CanManageCall || !callJoinMuted || inviteSelfUnmute);
        bool videoJoined = payload.VideoSourceGroups.Count > 0 && !videoStopped;
        string? videoEndpoint = payload.VideoSourceGroups.Count > 0
            ? joined.Transport.Video?.Endpoint
            : null;

        GroupCallJoinResult stored;
        using (TLDto.TLGroupCallParticipantState row = GroupCallJoinRows
                   .BuildParticipantRow(callId, access.CurrentUserId, mediaId,
                       payload.Source, now, muted, canSelfUnmute, videoJoined,
                       videoStopped, videoEndpoint))
        {
            stored = await _groupCallsRepository.TryJoinParticipantAsync(row);
        }

        if (stored.Status is not (GroupCallJoinStatus.Joined or GroupCallJoinStatus.Rejoined))
        {
            stored.Participant?.Dispose();
            stored.Call?.Dispose();
            await GroupCallJoinRows.ReleaseTransportAsync(_media, Log, callId, mediaId,
                "uncommitted join");
            return Error(GroupCallJoinRows.TranslateJoinFailure(stored.Status));
        }

        await UnitOfWork.SaveAsync();
        SourceMap.Replace(callId, joined.ViewerSources);

        using TLDto.TLGroupCallParticipantState participant = stored.Participant!.Value;
        using TLDto.TLGroupCallState updatedCall = stored.Call!.Value;

        byte[] chatBytes = ChatLink.SetCallFlags(access.Kind, access.ChatBytes!,
            callActive: true, callNotEmpty: true);
        await UnitOfWork.SaveAsync();
        int videoCount = await CountUnmutedVideoAsync(callId);

        var updates = new List<byte[]>(3);
        byte[] connectionParams = GroupCallJoinPayloadCodec.BuildConnectionParams(
            joined.Transport);
        using (TLUpdate connection = GroupCallBuilders.BuildConnectionUpdate(
                   connectionParams))
        {
            updates.Add(connection.AsSpan().ToArray());
        }

        GroupCallViewer viewer = await BuildViewerAsync(callId, access.CurrentUserId,
            access.CanManageCall);
        var selfOverlay = new GroupCallParticipantOverlay(MutedByYou: false,
            LocalVolume: null, joined.CanonicalSource,
            GroupCallJoinRows.BuildSelfSources(joined, payload, videoJoined));
        using (TLGroupCallParticipant selfRow = GroupCallBuilders.BuildParticipant(
                   participant, viewer, selfOverlay,
                   GroupCallParticipantDecoration.JustJoined |
                   GroupCallParticipantDecoration.Versioned))
        using (TLUpdate participants = BuildParticipantsUpdate(updatedCall,
                   selfRow.AsSpan()))
        {
            updates.Add(participants.AsSpan().ToArray());
        }
        updates.Add(BuildCallUpdateBytes(updatedCall, viewer, access.Peer.Id, videoCount));

        int delivered = await PushJoinToOtherMembersAsync(updatedCall, participant,
            access, videoCount);

        Log.Debug($"📞 joinGroupCall call:{callId} user:{access.CurrentUserId} " +
                  $"source:{payload.Source} media:{mediaId} rejoin:{rejoining} " +
                  $"video:{videoJoined} muted:{muted} fanout:{delivered}");
        return await BuildUnsequencedResultAsync(access.CurrentUserId, updates,
            chatBytes);
    }

    private static TLUpdatesResult Error(string message) =>
        (TLUpdatesResult)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    private static bool TryReadSelfJoinAs(InputPeerView peer, out long userId)
    {
        if (peer.Is(out InputPeerSelf _))
        {
            userId = 0;
            return true;
        }
        if (peer.Is(out InputPeerUser user) && user.UserId > 0)
        {
            userId = user.UserId;
            return true;
        }

        userId = 0;
        return false;
    }

    private static byte[] ReadParamsJson(DataJSONView view) =>
        view.Is(out DataJSON json) ? json.Data.ToArray() : Array.Empty<byte>();

    private readonly record struct InviteDecision(bool Valid, bool CanSelfUnmute);

    private async ValueTask<InviteDecision> ResolveInviteAsync(long callId,
        int currentGeneration, string hash)
    {
        using TLDto.TLGroupCallInvite? invite = await _groupCallsRepository
            .GetInviteByHashAsync(hash);
        if (invite == null)
        {
            return new InviteDecision(false, false);
        }

        var view = invite.Value.AsGroupCallInvite();
        if (view.CallId != callId || view.Revoked || view.Generation != currentGeneration)
        {
            return new InviteDecision(false, false);
        }
        if (view.Flags[2] && view.ExpiryDate <= Now())
        {
            return new InviteDecision(false, false);
        }

        return new InviteDecision(true, view.CanSelfUnmute);
    }

    private async Task<int> PushJoinToOtherMembersAsync(TLDto.TLGroupCallState call,
        TLDto.TLGroupCallParticipantState participant, GroupCallPeerAccess access,
        int videoCount)
    {
        long callId = call.AsGroupCallState().Id;
        var view = participant.AsGroupCallParticipantState();
        long joinerUserId = view.UserId;
        string producerMediaId = Encoding.UTF8.GetString(view.MediaId);

        int delivered = await Fanout.PushGroupCallUpdatesAsync(access.Peer.Id,
            access.CurrentUserId, async memberId =>
            {
                bool canManage = await CanManageCallAsync(access.Peer.Id, memberId);
                GroupCallViewer viewer = await BuildViewerAsync(callId, memberId,
                    canManage);
                string? viewerMediaId = await GetMediaIdAsync(callId, memberId);
                GroupCallParticipantOverlay overlay = await BuildMemberOverlayAsync(
                    callId, memberId, viewerMediaId, joinerUserId, producerMediaId);
                using TLGroupCallParticipant row = GroupCallBuilders.BuildParticipant(
                    participant, viewer, overlay,
                    GroupCallParticipantDecoration.JustJoined |
                    GroupCallParticipantDecoration.Versioned);
                return BuildParticipantsUpdate(call, row.AsSpan());
            });
        await PushCallUpdateToOtherMembersAsync(call, access.Peer.Id,
            access.CurrentUserId, videoCount);
        return delivered;
    }
}
