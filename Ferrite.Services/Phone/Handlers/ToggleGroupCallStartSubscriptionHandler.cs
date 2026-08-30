// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;
using TLUpdatesResult = Ferrite.TL.baseLayer.TLUpdates;

namespace Ferrite.Services.Phone.Handlers;

public sealed class ToggleGroupCallStartSubscriptionHandler : GroupCallHandlerBase
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    public ToggleGroupCallStartSubscriptionHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository,
        UpdateFanout fanout, GroupCallChatLink chatLink,
        IUpdatesContextFactory updatesContexts, IMTProtoTime time,
        GroupCallVideoOptions videoOptions, GroupCallMediaSourceMap sourceMap,
        ILogger log)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log)
    {
        _groupCallsRepository = groupCallsRepository;

    }

    [TLFunction(Constructors.baseLayer_ToggleGroupCallStartSubscription)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (ToggleGroupCallStartSubscription)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);
        bool subscribed = request.Subscribed;

        if (!callRead)
        {
            return Error(400, GroupCallErrors.GroupCallInvalid);
        }

        using GroupCallResolution resolution = await ResolveCallAsync(authKeyId, callId,
            accessHash, GroupCallAccessLevel.Participate);
        if (resolution.Error != null)
        {
            return Error(400, resolution.Error);
        }

        GroupCallPeerAccess access = resolution.Access!;
        int state = resolution.Call!.Value.AsGroupCallState().State;
        if (state == (int)GroupCallPersistenceState.Active)
        {
            return Error(403, GroupCallErrors.GroupCallAlreadyStarted);
        }
        if (state != (int)GroupCallPersistenceState.Scheduled)
        {
            return Error(400, GroupCallErrors.GroupCallInvalid);
        }

        GroupCallViewerMutationResult changed = await _groupCallsRepository
            .TrySetStartSubscriptionAsync(callId, access.CurrentUserId, subscribed);
        switch (changed.Status)
        {
            case GroupCallViewerMutationStatus.Updated:
                break;
            case GroupCallViewerMutationStatus.NoChange:
                return Error(400, GroupCallErrors.GroupCallNotModified);
            case GroupCallViewerMutationStatus.CallNotScheduled:
                return Error(403, GroupCallErrors.GroupCallAlreadyStarted);
            default:
                return Error(400, GroupCallErrors.GroupCallInvalid);
        }

        await UnitOfWork.SaveAsync();

        using TLDto.TLGroupCallState call = changed.Call!.Value;
        int videoCount = await CountUnmutedVideoAsync(callId);
        var viewer = new GroupCallViewer(access.CurrentUserId, access.CanManageCall,
            subscribed);
        byte[] update = BuildCallUpdateBytes(call, viewer, access.Peer.Id, videoCount);

        Log.Debug($"📞 toggleGroupCallStartSubscription call:{callId} " +
                  $"user:{access.CurrentUserId} subscribed:{subscribed}");
        return await BuildInvokerResultAsync(authKeyId, access.CurrentUserId,
            new[] { update }, access.ChatBytes!);
    }

    private static TLUpdatesResult Error(int code, string message) =>
        (TLUpdatesResult)RpcErrorGenerator.GenerateError(code,
            Encoding.UTF8.GetBytes(message));
}
