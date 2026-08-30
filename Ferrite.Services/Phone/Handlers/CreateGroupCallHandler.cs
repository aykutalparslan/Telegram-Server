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

public sealed class CreateGroupCallHandler : GroupCallHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IGroupCallsRepository _groupCallsRepository;

    private const int MaxTitleLength = 64;

    private readonly IdAllocators _ids;
    private readonly GroupCallActionMessages _actions;
    private readonly IGroupCallMediaPlane _media;
    private readonly IGroupCallBroadcastPlane _broadcast;

    public CreateGroupCallHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log,
        IdAllocators ids, GroupCallActionMessages actions, IGroupCallMediaPlane media,
        IGroupCallBroadcastPlane broadcast)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, fanout, chatLink, updatesContexts, time, videoOptions, sourceMap, log)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _groupCallsRepository = groupCallsRepository;

        _ids = ids;
        _actions = actions;
        _media = media;
        _broadcast = broadcast;
    }

    [TLFunction(Constructors.baseLayer_CreateGroupCall)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (CreateGroupCall)q;
        bool rtmpStream = request.RtmpStream;
        bool peerResolved = GroupCallAccess.TryResolveCallPeer(request.Get_PeerView(),
            out GroupCallPeerRef peer);
        int randomId = request.RandomId;
        bool hasTitle = request.Flags[0];
        string title = hasTitle ? Encoding.UTF8.GetString(request.Title) : string.Empty;
        bool hasScheduleDate = request.Flags[1];
        int scheduleDate = request.ScheduleDate;

        if (!peerResolved)
        {
            return Error(GroupCallErrors.PeerIdInvalid);
        }
        if (hasTitle && title.Length > MaxTitleLength)
        {
            return Error(GroupCallErrors.TitleInvalid);
        }

        int now = Now();
        if (hasScheduleDate && scheduleDate <= now)
        {
            return Error(GroupCallErrors.ScheduleDateInvalid);
        }

        GroupCallPeerAccess access = await GroupCallAccess.AuthorizeAsync(_authorizationRepository, _chatRepository, _chatParticipantsRepository, authKeyId, peer, GroupCallAccessLevel.Manage);
        if (access.Error != null)
        {
            return Error(access.Error);
        }

        long callId = await _ids.NextGroupCallIdAsync();
        long accessHash;
        do
        {
            accessHash = Random.Shared.NextInt64();
        } while (accessHash == 0);

        bool scheduled = hasScheduleDate;
        if (!scheduled && rtmpStream)
        {
            try
            {
                await _broadcast.CreateStreamAsync(callId, rtmpStream);
            }
            catch (GroupCallBroadcastException e)
            {
                Log.Warning(e, $"📡 createGroupCall could not allocate RTMP " +
                               $"stream for call:{callId}");
                return Error(GroupCallErrors.MediaUnavailable);
            }
        }
        else if (!scheduled)
        {
            try
            {
                await _media.CreateRoomAsync(callId);
            }
            catch (GroupCallMediaException e)
            {
                Log.Warning(e, $"📞 createGroupCall could not allocate a room for " +
                               $"call:{callId} peer:{peer.Type}/{peer.Id}");
                return Error(GroupCallErrors.MediaUnavailable);
            }
            try
            {
                await _broadcast.CreateStreamAsync(callId, rtmpStream: false);
            }
            catch (GroupCallBroadcastException e)
            {
                Log.Warning(e, $"📡 createGroupCall broadcast is degraded for " +
                               $"ordinary call:{callId}");
            }
        }

        GroupCallCreateResult created;
        using (TLDto.TLGroupCallState row = BuildCallRow(callId, accessHash, peer,
                   access.CurrentUserId, randomId, hasTitle, title, scheduled,
                   scheduleDate, now, rtmpStream))
        {
            created = await _groupCallsRepository.TryCreateCallAsync(row);
        }

        switch (created.Status)
        {
            case GroupCallCreateStatus.Created:
                break;
            case GroupCallCreateStatus.Idempotent:
                await ReleaseRoomAsync(scheduled, callId);
                using (TLDto.TLGroupCallState existing = created.Call!.Value)
                {
                    return await BuildReplayResultAsync(authKeyId, access, existing);
                }
            case GroupCallCreateStatus.ActiveCallExists:
                await ReleaseRoomAsync(scheduled, callId);
                return Error(GroupCallErrors.GroupCallAlreadyStarted);
            default:
                await ReleaseRoomAsync(scheduled, callId);
                return Error(GroupCallErrors.GroupCallInvalid);
        }

        using TLDto.TLGroupCallState call = created.Call!.Value;
        byte[] chatBytes = ChatLink.SetCallFlags(access.Kind, access.ChatBytes!,
            callActive: true, callNotEmpty: false);

        GroupCallViewer viewer = await BuildViewerAsync(callId, access.CurrentUserId,
            access.CanManageCall);
        byte[] callUpdate = BuildCallUpdateBytes(call, viewer, peer.Id,
            unmutedVideoCount: 0);
        await PushCallUpdateToOtherMembersAsync(call, peer.Id, access.CurrentUserId,
            unmutedVideoCount: 0);

        byte[] actionBytes = BuildActionBytes(callId, accessHash, scheduled, scheduleDate);
        Log.Debug($"📞 createGroupCall call:{callId} peer:{peer.Type}/{peer.Id} " +
                  $"creator:{access.CurrentUserId} scheduled:{scheduled}");
        return await _actions.EmitAsync(authKeyId, access.CurrentUserId, access.Kind,
            peer.Id, chatBytes, actionBytes, new[] { callUpdate });
    }

    private static TLUpdatesResult Error(string message) =>
        (TLUpdatesResult)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    private async Task ReleaseRoomAsync(bool scheduled, long callId)
    {
        if (scheduled)
        {
            return;
        }

        try
        {
            await _media.EndRoomAsync(callId);
        }
        catch (GroupCallMediaException e)
        {
            Log.Warning(e, $"📞 createGroupCall could not release the unused room for " +
                           $"call:{callId}");
        }
        try
        {
            await _broadcast.EndStreamAsync(callId);
        }
        catch (GroupCallBroadcastException e)
        {
            Log.Warning(e, $"📡 createGroupCall could not release the unused " +
                           $"broadcast stream for call:{callId}");
        }
    }

    private static TLDto.TLGroupCallState BuildCallRow(long callId, long accessHash,
        GroupCallPeerRef peer, long creatorUserId, int randomId, bool hasTitle,
        string title, bool scheduled, int scheduleDate, int now, bool rtmpStream)
    {
        var builder = TLDto.GroupCallState.Builder()
            .Id(callId)
            .AccessHash(accessHash)
            .PeerType((int)peer.Type)
            .PeerId(peer.Id)
            .CreatorUserId(creatorUserId)
            .RandomId(randomId)
            .State((int)(scheduled
                ? GroupCallPersistenceState.Scheduled
                : GroupCallPersistenceState.Active))
            .CreatedDate(now)
            .Version(1)
            .ParticipantsCount(0)
            .InviteGeneration(1)
            .MediaEpoch(1);
        if (rtmpStream)
        {
            builder = builder.RtmpStream(true);
        }
        if (hasTitle)
        {
            builder = builder.Title(Encoding.UTF8.GetBytes(title));
        }
        if (scheduled)
        {
            builder = builder.ScheduleDate(scheduleDate);
        }
        else
        {
            builder = builder.StartedDate(now);
        }

        return builder.Build();
    }

    private static byte[] BuildActionBytes(long callId, long accessHash, bool scheduled,
        int scheduleDate)
    {
        using TLInputGroupCall inputCall = GroupCallBuilders.BuildInputGroupCall(callId,
            accessHash);
        using TLMessageAction action = scheduled
            ? GroupCallActionMessages.BuildScheduledAction(inputCall, scheduleDate)
            : GroupCallActionMessages.BuildCallAction(inputCall);
        return action.AsSpan().ToArray();
    }

    private async ValueTask<TLUpdatesResult> BuildReplayResultAsync(long authKeyId,
        GroupCallPeerAccess access, TLDto.TLGroupCallState call)
    {
        long callId = call.AsGroupCallState().Id;
        GroupCallViewer viewer = await BuildViewerAsync(callId, access.CurrentUserId,
            access.CanManageCall);
        int videoCount = await CountUnmutedVideoAsync(callId);
        byte[] callUpdate = BuildCallUpdateBytes(call, viewer, access.Peer.Id, videoCount);
        return await BuildInvokerResultAsync(authKeyId, access.CurrentUserId,
            new[] { callUpdate }, access.ChatBytes!);
    }
}
