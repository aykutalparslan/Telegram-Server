// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public class PhoneCodeRepository : IPhoneCodeRepository
{
    private readonly IVolatileKVStore _store;
    public PhoneCodeRepository(IVolatileKVStore store)
    {
        _store = store;
        store.SetSchema(new TableDefinition("ferrite", "phone_codes_tl1",
            new KeyDefinition("pk",
                new DataColumn { Name = "phone_number", Type = DataType.String },
                new DataColumn { Name = "phone_code_hash", Type = DataType.String })));
    }
    public void PutPhoneCode(string phoneNumber, string phoneCodeHash, string phoneCode, TimeSpan expiresIn)
    {
        using var row = PhoneCode.Builder().Code(Encoding.UTF8.GetBytes(phoneCode)).Build();
        _store.Put(row.ToReadOnlySpan().ToArray(), expiresIn, phoneNumber, phoneCodeHash);
    }

    public string? GetPhoneCode(string phoneNumber, string phoneCodeHash)
    {
        var bytes = _store.Get(phoneNumber, phoneCodeHash);
        if (bytes is { Length: > 0 })
        {
            var value = new TLBytes(bytes, 0, bytes.Length);
            if (value.Constructor != Constructors.baseLayer_PhoneCode)
                throw new InvalidDataException("Phone-code codec/version mismatch.");
            return Encoding.UTF8.GetString(((TLPhoneCode)value).AsPhoneCode().Code);
        }

        return null;
    }

    public bool DeletePhoneCode(string phoneNumber, string phoneCodeHash)
    {
        _store.Delete(phoneNumber, phoneCodeHash);
        return true;
    }
}
