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

public sealed class GetBlockedHandler : ContactsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IBlockedPeersRepository _blockedPeersRepository;
    private readonly IUserRepository _userRepository;

    public GetBlockedHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IBlockedPeersRepository blockedPeersRepository, IContactsRepository contactsRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory)
        : base(unitOfWork, contactsRepository, userRepository, userStatusRepository, search, updates, updatesContextFactory)
    {
        _authorizationRepository = authorizationRepository;
        _blockedPeersRepository = blockedPeersRepository;
        _userRepository = userRepository;

    }

    [TLFunction(Constructors.baseLayer_GetBlocked)]
    public async Task<TLBlocked> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return EmptyBlocked();
            }

            var request = new GetBlocked(q.AsSpan());
            int offset = request.Offset;
            int limit = request.Limit;
            bool myStoriesFrom = request.MyStoriesFrom;

            if (myStoriesFrom)
            {
                return EmptyBlocked();
            }

            var blockedPeers = ReadBlockedPeerEntries(
                _blockedPeersRepository.GetBlockedPeers(auth.Value.AsAuthInfo().UserId));
            var page = PageBlockedPeers(blockedPeers, offset, limit);
            List<TLUser> userList = new ();
            foreach (var c in page)
            {
                if (c.PeerType == PeerType.User)
                {
                    var user = await GetUserInternal(auth.Value.AsAuthInfo().UserId, c.PeerId);
                    if(user != null) userList.Add(user.Value);
                }
            }
            var blocked = ToPeerBlockedVector(page);
            var users = ToUserVector(userList);
            if (ShouldReturnBlockedSlice(blockedPeers.Count, page.Count, offset, limit))
            {
                return BlockedSlice.Builder()
                    .Count(blockedPeers.Count)
                    .Users(users)
                    .Chats(new Vector())
                    .Blocked(blocked)
                    .Build();
            }

            return Blocked.Builder()
                .Users(users)
                .Chats(new Vector())
                .BlockedProperty(blocked)
                .Build();
        }
}
