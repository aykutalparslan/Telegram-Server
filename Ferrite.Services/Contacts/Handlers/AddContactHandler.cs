// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using DotNext.Collections.Generic;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.contacts;
using Ferrite.TL.baseLayer.dto;
using PeerBlocked = Ferrite.TL.baseLayer.PeerBlocked;

namespace Ferrite.Services.Handlers.ContactMethods;

public sealed class AddContactHandler : ContactsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IContactsRepository _contactsRepository;

    public AddContactHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IContactsRepository contactsRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory)
        : base(unitOfWork, userRepository, userStatusRepository, search, updates, updatesContextFactory)
    {
        _authorizationRepository = authorizationRepository;
        _contactsRepository = contactsRepository;

    }

    [TLFunction(Constructors.baseLayer_AddContact)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return AuthKeyInvalidUpdates();
            }

            var ownerUserId = auth.Value.AsAuthInfo().UserId;
            var request = new AddContact(q.AsSpan());
            long? contactUserId = GetInputUserId(request.Id, ownerUserId);
            byte[] firstName = request.FirstName.ToArray();
            byte[] lastName = request.LastName.ToArray();
            byte[] phone = request.Phone.ToArray();

            if (contactUserId == null || contactUserId <= 0)
            {
                return UserIdInvalidUpdates();
            }

            var contactUser = await GetUserInternal(contactUserId.Value);
            if (contactUser == null)
            {
                return UserIdInvalidUpdates();
            }

            using TLContactInfo contactInfo = ContactInfo.Builder()
                .UserId(contactUserId.Value)
                .Phone(phone)
                .ClientId(0)
                .FirstName(firstName)
                .LastName(lastName)
                .Date((int)DateTimeOffset.Now.ToUnixTimeSeconds())
                .Build();
            _contactsRepository.PutContact(ownerUserId, contactUserId.Value, contactInfo);
            await _unitOfWork.SaveAsync();

            using TLPeerSettings settings = PeerSettings.Builder().Build();
            return await BuildPeerSettingsUpdates(authKeyId, ownerUserId, contactUserId.Value,
                new List<TLUser> { contactUser.Value }, settings);
        }
}
