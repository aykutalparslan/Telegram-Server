// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.Services.Calls.E2E;
using Ferrite.TL;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLUpdatesResult = Ferrite.TL.baseLayer.TLUpdates;

namespace Ferrite.Services.Phone.Handlers;

/// <summary>
/// phone.getGroupCallChainBlocks. The reply must be a bare updates container
/// holding EXACTLY ONE updateGroupCallChainBlocks: pinned TDLib reads
/// updates[0] and gives up on anything else, which is how a conference silently
/// fails to start.
/// </summary>
public sealed class GetGroupCallChainBlocksHandler : ConferenceCallHandlerBase
{
    public GetGroupCallChainBlocksHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, IMessageRepository messageRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log, IGroupCallChainService chain)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, messageRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log, chain)
    {
    }

    [TLFunction(Constructors.baseLayer_GetGroupCallChainBlocks)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetGroupCallChainBlocks)q;
        // An invitee polls the head from its invite message: this read is what
        // it does BEFORE it has ever named the call by id and access hash.
        bool callRead = TryReadConferenceRef(request.Get_CallView(), out ConferenceCallRef reference);
        int subChainId = request.SubChainId;
        int offset = request.Offset;
        int limit = request.Limit;

        if (!callRead)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }
        // Only sub-chains 0 and 1 exist. TDLib refuses anything else client-side,
        // so a value outside that range is a raw-client error.
        if (subChainId is not (GroupCallSubChain.Blocks or GroupCallSubChain.Broadcast))
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }

        using ConferenceResolution resolution = await ResolveConferenceAsync(authKeyId,
            reference);
        if (resolution.Error != null)
        {
            return Error(resolution.Error);
        }

        long callId = resolution.Call!.Value.AsGroupCallState().Id;

        // This read is what makes joining possible at all: the client asks for the
        // head BEFORE it has a participant row, so participation cannot gate it.
        // The access hash is the capability, exactly as it is for getGroupCall.
        byte[] blocks = await BuildChainBlocksBytesAsync(callId, resolution.AccessHash,
            subChainId, offset, limit);
        return await BuildConferenceResultAsync(authKeyId, resolution.CurrentUserId,
            new[] { blocks });
    }
}
