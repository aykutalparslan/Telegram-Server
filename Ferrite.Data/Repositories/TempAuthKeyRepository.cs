// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

public class TempAuthKeyRepository : ITempAuthKeyRepository
{
    private readonly IVolatileKVStore _store;
    public TempAuthKeyRepository(IVolatileKVStore store)
    {
        _store = store;
        store.SetSchema(new TableDefinition("ferrite", "temp_auth_keys",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long })));
    }
    public bool PutTempAuthKey(long tempAuthKeyId, byte[] tempAuthKey, TimeSpan? expiresIn = null)
    {
        _store.Put(tempAuthKey, expiresIn, tempAuthKeyId);
        return true;
    }

    public bool DeleteTempAuthKey(long tempAuthKeyId)
    {
        _store.Delete(tempAuthKeyId);
        return true;
    }

    public byte[]? GetTempAuthKey(long tempAuthKeyId)
    {
        return _store.Get(tempAuthKeyId);
    }
    
    public async ValueTask<byte[]?> GetTempAuthKeyAsync(long tempAuthKeyId)
    {
        return await _store.GetAsync(tempAuthKeyId);
    }
}