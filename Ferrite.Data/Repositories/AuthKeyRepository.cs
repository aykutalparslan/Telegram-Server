// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

public class AuthKeyRepository : IAuthKeyRepository
{
    private readonly IKVStore _store;
    private readonly IVolatileKVStore _storeTemp;
    public AuthKeyRepository(IKVStore store, IVolatileKVStore storeTemp)
    {
        _store = store;
        _store.SetSchema(new TableDefinition("ferrite", "auth_keys",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long })));
        _storeTemp = storeTemp;
        _storeTemp.SetSchema(new TableDefinition("ferrite", "auth_keys",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long })));
    }
    public bool PutAuthKey(long authKeyId, byte[] authKey)
    {
        _storeTemp.Put(authKey, null, authKeyId);
        return _store.Put(authKey, authKeyId);
    }

    public byte[]? GetAuthKey(long authKeyId)
    {
        var val = _storeTemp.Get(authKeyId);
        if (val == null)
        {
            val = _store.Get(authKeyId);
            if (val != null)
            {
                _storeTemp.Put(val, null, authKeyId);
            }
        }
        return val;
    }

    public async ValueTask<byte[]?> GetAuthKeyAsync(long authKeyId)
    {
        var val = await _storeTemp.GetAsync(authKeyId);
        if (val == null)
        {
            val = await _store.GetAsync(authKeyId);
            if (val != null)
            {
                _storeTemp.Put(val, null, authKeyId);
            }
        }
        return val;
    }

    public bool DeleteAuthKey(long authKeyId)
    {
        _storeTemp.Delete(authKeyId);
        return  _store.Delete(authKeyId);
    }
}