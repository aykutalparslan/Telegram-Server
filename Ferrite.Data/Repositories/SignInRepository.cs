// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

public class SignInRepository : ISignInRepository
{
    private readonly IVolatileKVStore _store;
    public SignInRepository(IVolatileKVStore store)
    {
        _store = store;
        store.SetSchema(new TableDefinition("ferrite", "sign_ins_tl1",
            new KeyDefinition("pk",
                new DataColumn { Name = "phone_number", Type = DataType.String },
                new DataColumn { Name = "phone_code_hash", Type = DataType.String })));
    }
    public bool PutSignIn(long authKeyId, string phoneNumber, string phoneCodeHash)
    {
        using var row = SignInAuthKey.Builder().AuthKeyId(authKeyId).Build();
        _store.Put(row.ToReadOnlySpan().ToArray(), null, phoneNumber, phoneCodeHash);
        return true;
    }

    public long GetSignIn(string phoneNumber, string phoneCodeHash)
    {
        var result = _store.Get(phoneNumber, phoneCodeHash);
        if (result == null) return 0;
        var value = new TLBytes(result, 0, result.Length);
        if (value.Constructor != Constructors.baseLayer_SignInAuthKey)
            throw new InvalidDataException("Sign-in codec/version mismatch.");
        return ((TLSignInAuthKey)value).AsSignInAuthKey().AuthKeyId;
    }
}
