// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

public class BoundAuthKeyRepository : IBoundAuthKeyRepository
{
    private readonly IVolatileKVStore _storeTemp;
    private readonly IVolatileKVStore _storeAuth;
    private readonly IVolatileKVStore _storeBound;
    public BoundAuthKeyRepository(IVolatileKVStore storeTemp, IVolatileKVStore storeAuth,
         IVolatileKVStore storeBound)
    {
        _storeTemp = storeTemp;
        _storeAuth = storeAuth;
        _storeBound = storeBound;
        _storeTemp.SetSchema(new TableDefinition("ferrite", "bound_auth_keys_temp_tl1",
            new KeyDefinition("pk",
                new DataColumn { Name = "temp_auth_key_id", Type = DataType.Long })));
        _storeAuth.SetSchema(new TableDefinition("ferrite", "bound_auth_keys_auth_tl1",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long })));
        _storeBound.SetSchema(new TableDefinition("ferrite", "bound_auth_keys_by_auth_tl1",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long })));
    }
    
    public bool PutBoundAuthKey(long tempAuthKeyId, long authKeyId, TimeSpan expiresIn)
    {
        using var auth = BoundAuthKey.Builder().AuthKeyId(authKeyId).Build();
        using var temp = BoundTempAuthKey.Builder().TempAuthKeyId(tempAuthKeyId).Build();
        _storeTemp.Put(auth.ToReadOnlySpan().ToArray(), expiresIn, tempAuthKeyId);
        // each auth key can be bound to a single temp auth key at any given time
        _storeAuth.Put(temp.ToReadOnlySpan().ToArray(), expiresIn, authKeyId);
        // we need to retrieve a list of keys that was bound to an auth key in the given timeframe
        _storeBound.ListAdd(DateTimeOffset.Now.ToUnixTimeMilliseconds() + (long)expiresIn.TotalMilliseconds,
            temp.ToReadOnlySpan().ToArray(), expiresIn, authKeyId);
        return true;
    }

    public long? GetBoundAuthKey(long tempAuthKeyId)
    {
        var authBytes = _storeTemp.Get(tempAuthKeyId);
        if (authBytes == null)
        {
            return null;
        }
        var authKeyId = ReadAuthKey(authBytes);
        var tempBytes = _storeAuth.Get(authKeyId);
        if (tempBytes == null)
        {
            return null;
        }
        var boundKey = ReadTempAuthKey(tempBytes);
        if (boundKey == tempAuthKeyId)
        {
            return authKeyId;
        }
        return null;
    }

    public async ValueTask<long?> GetBoundAuthKeyAsync(long tempAuthKeyId)
    {
        var authBytes = await _storeTemp.GetAsync(tempAuthKeyId);
        if (authBytes == null)
        {
            return null;
        }
        var authKeyId = ReadAuthKey(authBytes);
        var tempBytes = await _storeAuth.GetAsync(authKeyId);
        if (tempBytes == null)
        {
            return null;
        }
        var boundKey = ReadTempAuthKey(tempBytes);
        if (boundKey == tempAuthKeyId)
        {
            return authKeyId;
        }
        return null;
    }

    public IReadOnlyList<long> GetTempAuthKeys(long authKeyId)
    {
        _storeBound.ListDeleteByScore(DateTimeOffset.Now.ToUnixTimeMilliseconds(), authKeyId);
        var queryResult = _storeBound.ListGet(authKeyId);
        List<long> result = new List<long>();
        foreach (var v in queryResult)
        {
            result.Add(ReadTempAuthKey(v));
        }
        return result;
    }

    private static long ReadAuthKey(byte[] bytes)
    {
        var value = new TLBytes(bytes, 0, bytes.Length);
        if (value.Constructor != Constructors.baseLayer_BoundAuthKey)
            throw new InvalidDataException("Bound auth-key codec/version mismatch.");
        return ((TLBoundAuthKey)value).AsBoundAuthKey().AuthKeyId;
    }

    private static long ReadTempAuthKey(byte[] bytes)
    {
        var value = new TLBytes(bytes, 0, bytes.Length);
        if (value.Constructor != Constructors.baseLayer_BoundTempAuthKey)
            throw new InvalidDataException("Bound temp-auth-key codec/version mismatch.");
        return ((TLBoundTempAuthKey)value).AsBoundTempAuthKey().TempAuthKeyId;
    }
}
