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

public sealed class GetContactIdsHandler : ContactsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IContactsRepository _contactsRepository;

    public GetContactIdsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IContactsRepository contactsRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory)
        : base(unitOfWork, contactsRepository, userRepository, userStatusRepository, search, updates, updatesContextFactory)
    {
        _authorizationRepository = authorizationRepository;
        _contactsRepository = contactsRepository;

    }

    [TLFunction(Constructors.baseLayer_GetContactIDs)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
        {
            var result = new List<long>();
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return ToLongVector(result);
            }

            var contactList = _contactsRepository.GetContacts(auth.Value.AsAuthInfo().UserId);
            foreach (var c in contactList)
            {
                result.Add(c.AsContact().UserId);
            }

            return ToLongVector(result);
        }
}
