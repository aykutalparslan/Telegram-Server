// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IContactsRepository
{
    public TLImportedContact PutContact(long userId, long contactUserId, TLContactInfo contact);
    public bool PutSavedContact(long userId, TLContactInfo contact);
    public bool HasContact(long userId, long contactUserId);
    public bool DeleteContact(long userId, long contactUserId);
    public bool DeleteSavedContact(long userId, string phone);
    public bool DeleteSavedContacts(long userId);
    public bool DeleteContacts(long userId);
    public IReadOnlyList<TLSavedContact> GetSavedContacts(long userId);
    public IReadOnlyList<TLContact> GetContacts(long userId);
    public IReadOnlyList<long> GetContactOwners(long contactUserId);
}
