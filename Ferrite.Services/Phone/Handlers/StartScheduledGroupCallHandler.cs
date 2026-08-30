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

public sealed class StartScheduledGroupCallHandler : GroupCallHandlerBase
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    private readonly GroupCallActionMessages _actions;
    private readonly IGroupCallMediaPlane _media;
    private readonly IGroupCallBroadcastPlane _broadcast;

    public StartScheduledGroupCallHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log,
        GroupCallActionMessages actions, IGroupCallMediaPlane media,
        IGroupCallBroadcastPlane broadcast)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log)
    {
        _groupCallsRepository = groupCallsRepository;

        _actions = actions;
        _media = media;
        _broadcast = broadcast;
    }

    [TLFunction(Constructors.baseLayer_StartScheduledGroupCall)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (StartScheduledGroupCall)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);

        if (!callRead)
        {
            return Error(400, GroupCallErrors.GroupCallInvalid);
        }

        using GroupCallResolution resolution = await ResolveCallAsync(authKeyId, callId,
            accessHash, GroupCallAccessLevel.Manage);
        if (resolution.Error != null)
        {
            return Error(400, resolution.Error);
        }

        GroupCallPeerAccess access = resolution.Access!;
        var callView = resolution.Call!.Value.AsGroupCallState();
        int state = callView.State;
        bool rtmpStream = callView.RtmpStream;
        if (state == (int)GroupCallPersistenceState.Active)
        {
            return Error(403, GroupCallErrors.GroupCallAlreadyStarted);
        }
        if (state != (int)GroupCallPersistenceState.Scheduled)
        {
            return Error(400, GroupCallErrors.GroupCallInvalid);
        }

        if (rtmpStream)
        {
            try
            {
                await _broadcast.CreateStreamAsync(callId, rtmpStream: true);
            }
            catch (GroupCallBroadcastException e)
            {
                Log.Warning(e, $"📡 startScheduledGroupCall could not allocate " +
                               $"RTMP stream for call:{callId}");
                return Error(400, GroupCallErrors.MediaUnavailable);
            }
        }
        else
        {
            try
            {
                await _media.CreateRoomAsync(callId);
            }
            catch (GroupCallMediaException e)
            {
                Log.Warning(e, $"📞 startScheduledGroupCall could not allocate a room " +
                               $"for call:{callId} kind:{e.Kind}");
                return Error(400, GroupCallErrors.MediaUnavailable);
            }
            try
            {
                await _broadcast.CreateStreamAsync(callId, rtmpStream: false);
            }
            catch (GroupCallBroadcastException e)
            {
                Log.Warning(e, $"📡 startScheduledGroupCall broadcast is degraded " +
                               $"for ordinary call:{callId}");
            }
        }

        GroupCallMutationResult started;
        try
        {
            started = await _groupCallsRepository.TryStartScheduledAsync(
                callId, Now());
        }
        catch
        {
            await ReleaseRoomAfterLostStartAsync(callId);
            throw;
        }

        if (started.Status != GroupCallMutationStatus.Updated)
        {
            started.Call?.Dispose();
            using TLDto.TLGroupCallState? current = await _groupCallsRepository.GetCallAsync(callId);
            if (current != null && current.Value.AsGroupCallState().State ==
                (int)GroupCallPersistenceState.Active)
            {
                return Error(403, GroupCallErrors.GroupCallAlreadyStarted);
            }

            await EndUnusedRoomAsync(callId);
            return Error(400, GroupCallErrors.GroupCallInvalid);
        }

        await UnitOfWork.SaveAsync();

        using TLDto.TLGroupCallState call = started.Call!.Value;
        byte[] chatBytes = ChatLink.SetCallFlags(access.Kind, access.ChatBytes!,
            callActive: true, callNotEmpty: false);
        int videoCount = await CountUnmutedVideoAsync(callId);
        GroupCallViewer viewer = await BuildViewerAsync(callId, access.CurrentUserId,
            access.CanManageCall);
        byte[] callUpdate = BuildCallUpdateBytes(call, viewer, access.Peer.Id,
            videoCount);

        await PushCallUpdateToOtherMembersAsync(call, access.Peer.Id,
            access.CurrentUserId, videoCount);

        byte[] actionBytes = BuildStartedActionBytes(call);
        Log.Debug($"📞 startScheduledGroupCall call:{callId} " +
                  $"peer:{access.Peer.Type}/{access.Peer.Id} " +
                  $"by:{access.CurrentUserId}");
        return await _actions.EmitAsync(authKeyId, access.CurrentUserId, access.Kind,
            access.Peer.Id, chatBytes, actionBytes, new[] { callUpdate });
    }

    private async Task ReleaseRoomAfterLostStartAsync(long callId)
    {
        try
        {
            using TLDto.TLGroupCallState? current = await _groupCallsRepository.GetCallAsync(callId);
            if (current != null && current.Value.AsGroupCallState().State ==
                (int)GroupCallPersistenceState.Active)
            {
                return;
            }
        }
        catch (Exception e)
        {
            Log.Warning(e, $"📞 startScheduledGroupCall could not determine " +
                           $"room ownership for call:{callId}");
            return;
        }

        await EndUnusedRoomAsync(callId);
    }

    private async Task EndUnusedRoomAsync(long callId)
    {
        try
        {
            await _media.EndRoomAsync(callId);
        }
        catch (GroupCallMediaException e)
        {
            Log.Warning(e, $"📞 startScheduledGroupCall could not release the " +
                           $"unused room for call:{callId}");
        }
        try
        {
            await _broadcast.EndStreamAsync(callId);
        }
        catch (GroupCallBroadcastException e)
        {
            Log.Warning(e, $"📡 startScheduledGroupCall could not release the " +
                           $"unused broadcast stream for call:{callId}");
        }
    }

    private static byte[] BuildStartedActionBytes(TLDto.TLGroupCallState call)
    {
        using TLInputGroupCall inputCall = GroupCallBuilders.BuildInputGroupCall(call);
        using TLMessageAction action = GroupCallActionMessages.BuildCallAction(inputCall);
        return action.AsSpan().ToArray();
    }

    private static TLUpdatesResult Error(int code, string message) =>
        (TLUpdatesResult)RpcErrorGenerator.GenerateError(code,
            Encoding.UTF8.GetBytes(message));
}
