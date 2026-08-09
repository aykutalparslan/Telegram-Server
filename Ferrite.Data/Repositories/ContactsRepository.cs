// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public class ContactsRepository : IContactsRepository
{
    private readonly IKVStore _store;
    private readonly IKVStore _storeMutual;
    public ContactsRepository(IKVStore store, IKVStore storeMutual)
    {
        _store = store;
        store.SetSchema(new TableDefinition("ferrite", "contacts",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "contact_user_id", Type = DataType.Long })));
        _storeMutual = storeMutual;
        _storeMutual.SetSchema(new TableDefinition("ferrite", "contacts_mutual_tl1",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "contact_user_id", Type = DataType.Long })));
    }
    public TLImportedContact PutContact(long userId, long contactUserId, TLContactInfo contact)
    {
        _store.Put(contact.AsSpan().ToArray(), userId, contactUserId);
        if (contact.AsContactInfo().UserId > 0)
        {
            using var owner = ContactOwnerReference.Builder().UserId(userId).Build();
            _storeMutual.Put(owner.ToReadOnlySpan().ToArray(), contactUserId, userId);
        }

        var c = contact.AsContactInfo();
        return ImportedContact.Builder()
            .ClientId(c.ClientId)
            .UserId(c.UserId)
            .Build();
    }

    public bool PutSavedContact(long userId, TLContactInfo contact)
    {
        var contactInfo = contact.AsContactInfo();
        return _store.Put(contact.AsSpan().ToArray(), userId, SavedContactKey(contactInfo.Phone));
    }

    public bool HasContact(long userId, long contactUserId)
    {
        return _store.Get(userId, contactUserId) != null;
    }

    public bool DeleteContact(long userId, long contactUserId)
    {
        bool deleted = _store.Delete(userId, contactUserId);
        _storeMutual.Delete(contactUserId, userId);
        return deleted;
    }

    public bool DeleteSavedContact(long userId, string phone)
    {
        return _store.Delete(userId, SavedContactKey(phone));
    }

    public bool DeleteSavedContacts(long userId)
    {
        var deleted = true;
        var iter = _store.Iterate(userId);
        foreach (var savedBytes in iter)
        {
            var contactInfo = new TLContactInfo(savedBytes, 0, savedBytes.Length)
                .AsContactInfo();
            if (contactInfo.UserId <= 0)
            {
                deleted &= _store.Delete(userId, SavedContactKey(contactInfo.Phone));
            }
        }

        return deleted;
    }

    public bool DeleteContacts(long userId)
    {
        foreach (var contactBytes in _store.Iterate(userId))
        {
            var contact = new TLContactInfo(contactBytes, 0, contactBytes.Length)
                .AsContactInfo();
            if (contact.UserId > 0)
            {
                _storeMutual.Delete(contact.UserId, userId);
            }
        }

        return _store.Delete(userId);
    }

    public IReadOnlyList<TLSavedContact> GetSavedContacts(long userId)
    {
        List<TLSavedContact> savedContacts = new();
        var iter = _store.Iterate(userId);
        foreach (var savedBytes in iter)
        {
            var contactInfo = new TLContactInfo(savedBytes, 0, savedBytes.Length)
                .AsContactInfo();
            savedContacts.Add(SavedPhoneContact.Builder()
                .Phone(contactInfo.Phone)
                .FirstName(contactInfo.FirstName)
                .LastName(contactInfo.LastName)
                .Date(contactInfo.Date)
                .Build());
        }

        return savedContacts;
    }

    public IReadOnlyList<TLContact> GetContacts(long userId)
    {
        List<TLContact> contacts = new();
        var contactsIterator = _store.Iterate(userId);
        List<long> mutualContacts = new ();
        var mutualIterator = _storeMutual.Iterate(userId);
        foreach (var c in mutualIterator)
        {
            mutualContacts.Add(ReadContactOwner(c));
        }
        foreach (var savedBytes in contactsIterator)
        {
            var contact = new TLContactInfo(savedBytes, 0, savedBytes.Length).AsContactInfo();
            if (contact.UserId <= 0)
            {
                continue;
            }

            var mutual = mutualContacts.Contains(contact.UserId);
            contacts.Add(Contact.Builder()
                .UserId(contact.UserId)
                .Mutual(mutual)
                .Build());
        }

        return contacts;
    }

    public IReadOnlyList<long> GetContactOwners(long contactUserId)
    {
        List<long> owners = new();
        foreach (var ownerBytes in _storeMutual.Iterate(contactUserId))
        {
            owners.Add(ReadContactOwner(ownerBytes));
        }

        return owners;
    }

    private static long SavedContactKey(ReadOnlySpan<byte> phone)
    {
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;

        var hash = offset;
        foreach (var b in phone)
        {
            hash ^= b;
            hash *= prime;
        }

        return -1 - (long)(hash & 0x7fffffffffffffffUL);
    }

    private static long SavedContactKey(string phone)
    {
        return SavedContactKey(System.Text.Encoding.UTF8.GetBytes(phone));
    }

    private static long ReadContactOwner(byte[] bytes)
    {
        var value = new TLBytes(bytes, 0, bytes.Length);
        if (value.Constructor != Constructors.baseLayer_ContactOwnerReference)
            throw new InvalidDataException("Contact-owner codec/version mismatch.");
        return ((TLContactOwnerReference)value).AsContactOwnerReference().UserId;
    }
}
