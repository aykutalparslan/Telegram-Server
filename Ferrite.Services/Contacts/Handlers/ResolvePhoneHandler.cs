// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using DotNext.Collections.Generic;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.contacts;
using Ferrite.TL.baseLayer.dto;
using PeerBlocked = Ferrite.TL.baseLayer.PeerBlocked;

namespace Ferrite.Services.Handlers.ContactMethods;

public sealed class ResolvePhoneHandler : ContactsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IUserRepository _userRepository;

    public ResolvePhoneHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IContactsRepository contactsRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory)
        : base(unitOfWork, contactsRepository, userRepository, userStatusRepository, search, updates, updatesContextFactory)
    {
        _authorizationRepository = authorizationRepository;
        _userRepository = userRepository;

    }

    [TLFunction(Constructors.baseLayer_ResolvePhone)]
    public async Task<TLResolvedPeer> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return AuthKeyInvalidResolvedPeer();
            }

            var phone = Encoding.UTF8.GetString(new ResolvePhone(q.AsSpan()).Phone);
            var found = _userRepository.GetUser(phone);
            if (found == null)
            {
                return PhoneNotOccupiedResolvedPeer();
            }

            var peerUser = await WithStatus(auth.Value.AsAuthInfo().UserId, found.Value);
            using TLPeer peer = new PeerUser(peerUser.AsUser().Id);
            return ResolvedPeer.Builder()
                .Peer(peer.AsSpan())
                .Users(ToUserVector(new List<TLUser> { peerUser }))
                .Chats(new Vector())
                .Build();
        }
}
