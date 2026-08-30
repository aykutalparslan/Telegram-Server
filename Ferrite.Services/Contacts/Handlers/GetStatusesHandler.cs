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

public sealed class GetStatusesHandler : ContactsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IContactsRepository _contactsRepository;
    private readonly IUserStatusRepository _userStatusRepository;

    public GetStatusesHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IContactsRepository contactsRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory)
        : base(unitOfWork, contactsRepository, userRepository, userStatusRepository, search, updates, updatesContextFactory)
    {
        _authorizationRepository = authorizationRepository;
        _contactsRepository = contactsRepository;
        _userStatusRepository = userStatusRepository;

    }

    [TLFunction(Constructors.baseLayer_GetStatuses)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            var result =  new List<TLContactStatus>();
            if (auth == null) return ToContactStatusVector(result);
            var contactList = _contactsRepository.GetContacts(auth.Value.AsAuthInfo().UserId);

            foreach (var c in contactList)
            {
                var userId = c.AsContact().UserId;
                var status = await _userStatusRepository.GetUserStatusAsync(userId);
                TLContactStatus contactStatus = ContactStatus.Builder()
                    .UserId(userId)
                    .Status(status.AsSpan())
                    .Build();
                result.Add(contactStatus);
            }
            return ToContactStatusVector(result);
        }
}
