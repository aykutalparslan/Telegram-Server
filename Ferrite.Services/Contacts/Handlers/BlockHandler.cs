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

public sealed class BlockHandler : ContactsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IBlockedPeersRepository _blockedPeersRepository;

    public BlockHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IBlockedPeersRepository blockedPeersRepository, IContactsRepository contactsRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory)
        : base(unitOfWork, contactsRepository, userRepository, userStatusRepository, search, updates, updatesContextFactory)
    {
        _authorizationRepository = authorizationRepository;
        _blockedPeersRepository = blockedPeersRepository;

    }

    [TLFunction(Constructors.baseLayer_Block)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return AuthKeyInvalidBool();
            }

            var ownerUserId = auth.Value.AsAuthInfo().UserId;
            var request = new Block(q.AsSpan());
            bool myStoriesFrom = request.MyStoriesFrom;
            BlockedPeerKey? blockedPeer = GetBlockedPeer(request.Get_IdView(), ownerUserId);

            if (myStoriesFrom)
            {
                return new BoolTrue();
            }

            if (blockedPeer != null)
            {
                _blockedPeersRepository.PutBlockedPeer(ownerUserId,
                    blockedPeer.Value.PeerId, blockedPeer.Value.PeerType,
                    DateTimeOffset.Now);
            }

            var result = await _unitOfWork.SaveAsync();
            if (result && blockedPeer != null)
            {
                await EnqueuePeerBlockedUpdate(ownerUserId, blockedPeer.Value, blocked: true, myStoriesFrom: false);
            }

            return result ? new BoolTrue(): new BoolFalse();
        }
}
