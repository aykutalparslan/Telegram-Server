// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using StackExchange.Redis;

namespace Ferrite.Data.Repositories;

public class RedisDataStore : IVolatileKVStore, IDisposable
{
    private TableDefinition? _table;
    private readonly IConnectionMultiplexer _redis;
    private readonly bool _ownsConnection;

    public RedisDataStore(string config)
        : this(ConnectionMultiplexer.Connect(config), ownsConnection: true)
    {
    }

    internal RedisDataStore(IConnectionMultiplexer redis)
        : this(redis, ownsConnection: false)
    {
    }

    private RedisDataStore(IConnectionMultiplexer redis, bool ownsConnection)
    {
        _redis = redis;
        _ownsConnection = ownsConnection;
    }
    public void SetSchema(TableDefinition table)
    {
        _table = table;
    }

    public void Put(byte[] value, TimeSpan? ttl = null, params object[] keys)
    {
        IDatabase db = _redis.GetDatabase();
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        RedisKey key = primaryKey.ArrayValue;
        db.StringSet(key, (RedisValue)value, ttl.HasValue ? new Expiration(ttl.Value) : Expiration.Default);
    }

    public void UpdateTtl(TimeSpan? ttl = null, params object[] keys)
    {
        IDatabase db = _redis.GetDatabase();
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        RedisKey key = primaryKey.ArrayValue;
        db.KeyExpire(key, ttl);
    }

    public bool ListAdd(long score, byte[] value, TimeSpan? ttl = null, params object[] keys)
    {
        IDatabase db = _redis.GetDatabase();
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        RedisKey key = primaryKey.ArrayValue;
        db.SortedSetAdd(key, (RedisValue)value, score);
        if (ttl != null)
        {
            db.KeyExpire(key, ttl);
        }

        return true;
    }

    public bool ListDelete(byte[] value, params object[] keys)
    {
        IDatabase db = _redis.GetDatabase();
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        RedisKey key = primaryKey.ArrayValue;
        return db.SortedSetRemove(key, value);
    }

    public bool ListDeleteByScore(long score, params object[] keys)
    {
        IDatabase db = _redis.GetDatabase();
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        RedisKey key = primaryKey.ArrayValue;
        db.SortedSetRemoveRangeByScore(key, 0, score);
        return true;
    }

    public IList<byte[]> ListGet(params object[] keys)
    {
        IDatabase db = _redis.GetDatabase();
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        RedisKey key = primaryKey.ArrayValue;
        var result = db.SortedSetRangeByScore(key);
        var list = Array.ConvertAll<RedisValue, byte[]>(result, item => (byte[])item);
        return list;
    }

    public void Delete(params object[] keys)
    {
        IDatabase db = _redis.GetDatabase();
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        RedisKey key = primaryKey.ArrayValue;
        db.KeyDelete(key);
    }

    public bool Exists(params object[] keys)
    {
        IDatabase db = _redis.GetDatabase();
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        RedisKey key = primaryKey.ArrayValue;
        return db.KeyExists(key);
    }

    public byte[]? Get(params object[] keys)
    {
        IDatabase db = _redis.GetDatabase();
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        RedisKey key = primaryKey.ArrayValue;
        return db.StringGet(key);
    }

    public async ValueTask<byte[]?> GetAsync(params object[] keys)
    {
        object _asyncState = new object();
        IDatabase db = _redis.GetDatabase(asyncState: _asyncState);
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        RedisKey key = primaryKey.ArrayValue;
        return await db.StringGetAsync(key);
    }

    public void Dispose()
    {
        if (_ownsConnection) _redis.Dispose();
    }
}
