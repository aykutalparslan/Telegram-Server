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

public sealed class DeleteByPhonesHandler : ContactsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IContactsRepository _contactsRepository;
    private readonly IUserRepository _userRepository;

    public DeleteByPhonesHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IContactsRepository contactsRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory)
        : base(unitOfWork, contactsRepository, userRepository, userStatusRepository, search, updates, updatesContextFactory)
    {
        _authorizationRepository = authorizationRepository;
        _contactsRepository = contactsRepository;
        _userRepository = userRepository;

    }

    [TLFunction(Constructors.baseLayer_DeleteByPhones)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return AuthKeyInvalidBool();
            }

            var ownerUserId = auth.Value.AsAuthInfo().UserId;
            var phones = ToStringList(new DeleteByPhones(q.AsSpan()).Phones);
            foreach (var p in phones)
            {
                _contactsRepository.DeleteSavedContact(ownerUserId, p);

                var contactUserId = _userRepository.GetUserId(p);
                if (contactUserId != null)
                {
                    _contactsRepository.DeleteContact(ownerUserId, contactUserId.Value);
                }
            }
            await _unitOfWork.SaveAsync();

            return new BoolTrue();
        }
}
