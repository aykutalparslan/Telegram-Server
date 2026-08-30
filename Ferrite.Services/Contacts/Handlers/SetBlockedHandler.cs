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

public sealed class SetBlockedHandler : ContactsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IBlockedPeersRepository _blockedPeersRepository;

    public SetBlockedHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IBlockedPeersRepository blockedPeersRepository, IContactsRepository contactsRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory)
        : base(unitOfWork, contactsRepository, userRepository, userStatusRepository, search, updates, updatesContextFactory)
    {
        _authorizationRepository = authorizationRepository;
        _blockedPeersRepository = blockedPeersRepository;

    }

    [TLFunction(Constructors.baseLayer_SetBlocked)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return AuthKeyInvalidBool();
            }

            var ownerUserId = auth.Value.AsAuthInfo().UserId;
            var request = new SetBlocked(q.AsSpan());
            bool myStoriesFrom = request.MyStoriesFrom;
            List<BlockedPeerKey> requestedPeers = ToBlockedPeerKeys(request.Id, request.Limit, ownerUserId);

            if (myStoriesFrom)
            {
                return new BoolTrue();
            }

            var currentEntries = ReadBlockedPeerEntries(
                _blockedPeersRepository.GetBlockedPeers(ownerUserId));
            var currentPeers = currentEntries
                .Select(x => new BlockedPeerKey(x.PeerId, x.PeerType))
                .ToHashSet();
            var replacementPeers = requestedPeers.ToHashSet();

            foreach (var peer in currentPeers)
            {
                _blockedPeersRepository.DeleteBlockedPeer(ownerUserId, peer.PeerId, peer.PeerType);
            }

            var now = DateTimeOffset.Now;
            foreach (var peer in replacementPeers)
            {
                _blockedPeersRepository.PutBlockedPeer(ownerUserId, peer.PeerId, peer.PeerType, now);
            }

            var result = await _unitOfWork.SaveAsync();
            if (result)
            {
                foreach (var peer in replacementPeers.Except(currentPeers))
                {
                    await EnqueuePeerBlockedUpdate(ownerUserId, peer, blocked: true, myStoriesFrom: false);
                }

                foreach (var peer in currentPeers.Except(replacementPeers))
                {
                    await EnqueuePeerBlockedUpdate(ownerUserId, peer, blocked: false, myStoriesFrom: false);
                }
            }

            return result ? new BoolTrue() : new BoolFalse();
        }
}
