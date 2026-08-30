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

public sealed class AcceptContactHandler : ContactsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IContactsRepository _contactsRepository;
    private readonly IUserRepository _userRepository;

    public AcceptContactHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IContactsRepository contactsRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory)
        : base(unitOfWork, contactsRepository, userRepository, userStatusRepository, search, updates, updatesContextFactory)
    {
        _authorizationRepository = authorizationRepository;
        _contactsRepository = contactsRepository;
        _userRepository = userRepository;

    }

    [TLFunction(Constructors.baseLayer_AcceptContact)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return AuthKeyInvalidUpdates();
            }

            var ownerUserId = auth.Value.AsAuthInfo().UserId;
            var request = new AcceptContact(q.AsSpan());
            long? contactUserId = GetInputUserId(request.Id, ownerUserId);

            if (contactUserId == null || contactUserId <= 0)
            {
                return UserIdInvalidUpdates();
            }

            if (!_contactsRepository.HasContact(contactUserId.Value, ownerUserId))
            {
                return UserIdInvalidUpdates();
            }

            var contactUser = await GetUserInternal(ownerUserId, contactUserId.Value);
            if (contactUser == null)
            {
                return UserIdInvalidUpdates();
            }

            using var ownerUser = _userRepository.GetUser(ownerUserId);
            if (ownerUser == null)
            {
                return UserIdInvalidUpdates();
            }

            var owner = ownerUser.Value.AsUser();
            byte[] ownerPhone = owner.Phone.ToArray();
            byte[] ownerFirstName = owner.FirstName.ToArray();
            byte[] ownerLastName = owner.LastName.ToArray();

            var contact = contactUser.Value.AsUser();
            byte[] contactPhone = contact.Phone.ToArray();
            byte[] contactFirstName = contact.FirstName.ToArray();
            byte[] contactLastName = contact.LastName.ToArray();

            var now = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
            using TLContactInfo contactInfo = ContactInfo.Builder()
                .UserId(contactUserId.Value)
                .Phone(contactPhone)
                .ClientId(0)
                .FirstName(contactFirstName)
                .LastName(contactLastName)
                .Date(now)
                .Build();
            using TLContactInfo ownerInfo = ContactInfo.Builder()
                .UserId(ownerUserId)
                .Phone(ownerPhone)
                .ClientId(0)
                .FirstName(ownerFirstName)
                .LastName(ownerLastName)
                .Date(now)
                .Build();

            _contactsRepository.PutContact(ownerUserId, contactUserId.Value, contactInfo);
            _contactsRepository.PutContact(contactUserId.Value, ownerUserId, ownerInfo);
            await _unitOfWork.SaveAsync();

            using TLPeerSettings settings = PeerSettings.Builder().Build();
            return await BuildPeerSettingsUpdates(authKeyId, ownerUserId, contactUserId.Value,
                new List<TLUser> { contactUser.Value }, settings);
        }
}
