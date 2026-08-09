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

public sealed class ImportContactsHandler : ContactsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IContactsRepository _contactsRepository;
    private readonly IUserRepository _userRepository;

    public ImportContactsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IContactsRepository contactsRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory)
        : base(unitOfWork, userRepository, userStatusRepository, search, updates, updatesContextFactory)
    {
        _authorizationRepository = authorizationRepository;
        _contactsRepository = contactsRepository;
        _userRepository = userRepository;

    }

    [TLFunction(Constructors.baseLayer_ImportContacts)]
    public async Task<TLImportedContacts> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return EmptyImportedContacts();
            }

            List<TLImportedContact> importedContacts = new();
            List<TLUser> users = new();
            var userId = auth.Value.AsAuthInfo().UserId;
            var contacts = ToInputPhoneContactList(new ImportContacts(q.AsSpan()).Contacts);
            foreach (var c in contacts)
            {
                using var user = _userRepository.GetUser(c.Phone);
                if (user == null)
                {
                    using TLContactInfo savedContactInfo = ContactInfo.Builder()
                        .UserId(0)
                        .Phone(c.PhoneBytes)
                        .ClientId(c.ClientId)
                        .FirstName(c.FirstName)
                        .LastName(c.LastName)
                        .Date((int)DateTimeOffset.Now.ToUnixTimeSeconds())
                        .Build();
                    _contactsRepository.PutSavedContact(userId, savedContactInfo);
                    continue;
                }

                using TLContactInfo contactInfo = ContactInfo.Builder()
                    .UserId(user.Value.AsUser().Id)
                    .Phone(c.PhoneBytes)
                    .ClientId(c.ClientId)
                    .FirstName(c.FirstName)
                    .LastName(c.LastName)
                    .Date((int)DateTimeOffset.Now.ToUnixTimeSeconds())
                    .Build();
                var imported = _contactsRepository.PutContact(userId,
                    user.Value.AsUser().Id, contactInfo);
                var contactUser = _userRepository.GetUser(imported.AsImportedContact().UserId);
                if (contactUser == null)
                {
                    imported.Dispose();
                    continue;
                }

                users.Add(contactUser.Value);
                importedContacts.Add(imported);
            }

            await _unitOfWork.SaveAsync();

            return ImportedContacts.Builder()
                .Users(ToUserVector(users))
                .Imported(ToImportedContactVector(importedContacts))
                .PopularInvites(new Vector())
                .RetryContacts(new VectorOfLong())
                .Build();
        }
}
