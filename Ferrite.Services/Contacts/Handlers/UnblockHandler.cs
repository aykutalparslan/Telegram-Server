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

public sealed class UnblockHandler : ContactsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IBlockedPeersRepository _blockedPeersRepository;

    public UnblockHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IBlockedPeersRepository blockedPeersRepository, IContactsRepository contactsRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory)
        : base(unitOfWork, contactsRepository, userRepository, userStatusRepository, search, updates, updatesContextFactory)
    {
        _authorizationRepository = authorizationRepository;
        _blockedPeersRepository = blockedPeersRepository;

    }

    [TLFunction(Constructors.baseLayer_Unblock)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return AuthKeyInvalidBool();
            }

            var ownerUserId = auth.Value.AsAuthInfo().UserId;
            var request = new Unblock(q.AsSpan());
            bool myStoriesFrom = request.MyStoriesFrom;
            BlockedPeerKey? blockedPeer = GetBlockedPeer(request.Get_IdView(), ownerUserId);

            if (myStoriesFrom)
            {
                return new BoolTrue();
            }

            if (blockedPeer != null)
            {
                _blockedPeersRepository.DeleteBlockedPeer(ownerUserId,
                    blockedPeer.Value.PeerId, blockedPeer.Value.PeerType);
            }

            var result = await _unitOfWork.SaveAsync();
            if (result && blockedPeer != null)
            {
                await EnqueuePeerBlockedUpdate(ownerUserId, blockedPeer.Value, blocked: false, myStoriesFrom: false);
            }

            return result ? new BoolTrue(): new BoolFalse();
        }
}
