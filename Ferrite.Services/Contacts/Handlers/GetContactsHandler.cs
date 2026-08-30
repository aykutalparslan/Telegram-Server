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

public sealed class GetContactsHandler : ContactsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IContactsRepository _contactsRepository;
    private readonly IUserRepository _userRepository;

    public GetContactsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IContactsRepository contactsRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory)
        : base(unitOfWork, contactsRepository, userRepository, userStatusRepository, search, updates, updatesContextFactory)
    {
        _authorizationRepository = authorizationRepository;
        _contactsRepository = contactsRepository;
        _userRepository = userRepository;

    }

    [TLFunction(Constructors.baseLayer_GetContacts)]
    public async Task<TLContacts> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return EmptyContacts();
            }

            var contactList = _contactsRepository.GetContacts(auth.Value.AsAuthInfo().UserId);

            List<TLUser> userList = new ();
            foreach (var c in contactList)
            {
                var user = await GetUserInternal(auth.Value.AsAuthInfo().UserId, c.AsContact().UserId);
                if(user != null) userList.Add(user.Value);
            }

            return Ferrite.TL.baseLayer.contacts.Contacts.Builder()
                .ContactsProperty(ToContactVector(contactList.ToList()))
                .Users(ToUserVector(userList))
                .SavedCount(contactList.Count)
                .Build();
        }
}
