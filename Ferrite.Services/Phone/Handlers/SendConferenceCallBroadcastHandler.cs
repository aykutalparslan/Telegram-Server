// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.Services.Calls.E2E;
using Ferrite.TL;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLUpdatesResult = Ferrite.TL.baseLayer.TLUpdates;

namespace Ferrite.Services.Phone.Handlers;

public sealed class SendConferenceCallBroadcastHandler : ConferenceCallHandlerBase
{
    public SendConferenceCallBroadcastHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, IMessageRepository messageRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log, IGroupCallChainService chain)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, messageRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log, chain)
    {
    }

    [TLFunction(Constructors.baseLayer_SendConferenceCallBroadcast)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (SendConferenceCallBroadcast)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);
        byte[] block = request.Block.ToArray();

        if (!callRead)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }

        using ConferenceResolution resolution = await ResolveConferenceAsync(authKeyId,
            callId, accessHash);
        if (resolution.Error != null)
        {
            return Error(resolution.Error);
        }
        if (!resolution.IsParticipant)
        {
            return Error(GroupCallErrors.GroupCallJoinMissing);
        }

        GroupCallChainAppend appended = await Chain.TryAppendAsync(callId,
            GroupCallSubChain.Broadcast, resolution.CurrentUserId, block);
        if (appended.Error != ChainValidationError.None)
        {
            Log.Debug($"📞 sendConferenceCallBroadcast rejected a broadcast for " +
                      $"call:{callId} user:{resolution.CurrentUserId}: {appended.Error}");
            return Error(TranslateChainError(appended.Error));
        }

        byte[] blocksUpdate = await BuildChainBlocksBytesAsync(callId, accessHash,
            GroupCallSubChain.Broadcast, offset: appended.Height,
            limit: GroupCallChainService.MaxWindow);
        List<long> members = await GetConferenceMemberIdsAsync(callId,
            resolution.CurrentUserId);
        await Fanout.EnqueueSerializedAsync(members, new[] { blocksUpdate });

        Log.Debug($"📞 sendConferenceCallBroadcast call:{callId} " +
                  $"user:{resolution.CurrentUserId} offset:{appended.Height} " +
                  $"fanout:{members.Count}");
        return await BuildConferenceResultAsync(authKeyId, resolution.CurrentUserId,
            new[] { blocksUpdate });
    }
}
