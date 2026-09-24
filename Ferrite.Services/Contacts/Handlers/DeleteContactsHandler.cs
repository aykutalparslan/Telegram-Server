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

public sealed class DeleteContactsHandler : ContactsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IContactsRepository _contactsRepository;

    public DeleteContactsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IContactsRepository contactsRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory)
        : base(unitOfWork, contactsRepository, userRepository, userStatusRepository, search, updates, updatesContextFactory)
    {
        _authorizationRepository = authorizationRepository;
        _contactsRepository = contactsRepository;

    }

    [TLFunction(Constructors.baseLayer_DeleteContacts)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return AuthKeyInvalidUpdates();
            }

            var userId = auth.Value.AsAuthInfo().UserId;
            var id = ToInputUserIds(new DeleteContacts(q.AsSpan()).Id, userId);
            foreach (var contactUserId in id)
            {
                _contactsRepository.DeleteContact(userId, contactUserId);
            }

            await _unitOfWork.SaveAsync();

            List<TLUser> userList = new();
            List<TLUpdate> updateList = new();
            foreach (var contactUserId in id)
            {
                var contactUser = await GetUserInternal(userId, contactUserId);
                if (contactUser != null) userList.Add(contactUser.Value);
                using TLPeer peer = new PeerUser(contactUserId);
                using TLPeerSettings settings = PeerSettings.Builder()
                    .AddContact(true)
                    .Build();
                TLUpdate update = UpdatePeerSettings.Builder()
                    .Peer(peer.AsSpan())
                    .Settings(settings.AsSpan())
                    .Build();
                updateList.Add(update);
            }

            TLUpdates res = Ferrite.TL.baseLayer.Updates.Builder()
                .Users(ToUserVector(userList))
                .UpdatesProperty(ToUpdateVector(updateList))
                .Chats(new Vector())
                .Seq(0)
                .Date((int)DateTimeOffset.Now.ToUnixTimeSeconds())
                .Build();
            return res;
        }
}
