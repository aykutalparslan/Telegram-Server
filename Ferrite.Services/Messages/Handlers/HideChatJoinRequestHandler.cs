// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class HideChatJoinRequestHandler : ChatJoinRequestHandlerBase
{
    public HideChatJoinRequestHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChatInvitesRepository chatInvitesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IUpdatesContextFactory updatesContextFactory,
        ChatRowStore chatRows, InviteStore invites, MessageStore messages,
        UpdateFanout fanout, TimeProvider timeProvider)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, chatInvitesRepository, chatParticipantsRepository, chatRepository, userRepository, counterFactory, updatesContextFactory, chatRows, invites,
            messages, fanout, timeProvider)
    {
    }

    [TLFunction(Constructors.baseLayer_HideChatJoinRequest)]
    public Task<TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (HideChatJoinRequest)q;
        InvitePeerSelection peer = ReadInvitePeer(request.Get_PeerView());
        InviteUserSelection user = ReadInviteUser(request.Get_UserIdView());
        return HandleSingleAsync(authKeyId, peer, user, request.Approved);
    }
}
