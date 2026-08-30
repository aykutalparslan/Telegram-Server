// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class HideAllChatJoinRequestsHandler : ChatJoinRequestHandlerBase
{
    public HideAllChatJoinRequestsHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChatInvitesRepository chatInvitesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IUpdatesContextFactory updatesContextFactory,
        ChatRowStore chatRows, InviteStore invites, MessageStore messages,
        UpdateFanout fanout, TimeProvider timeProvider)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, chatInvitesRepository, chatParticipantsRepository, chatRepository, userRepository, counterFactory, updatesContextFactory, chatRows, invites,
            messages, fanout, timeProvider)
    {
    }

    [TLFunction(Constructors.baseLayer_HideAllChatJoinRequests)]
    public Task<TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (HideAllChatJoinRequests)q;
        InvitePeerSelection peer = ReadInvitePeer(request.Get_PeerView());
        string? link = request.Flags[1]
            ? Encoding.UTF8.GetString(request.Link)
            : null;
        return HandleAllAsync(authKeyId, peer, request.Approved, link);
    }
}
