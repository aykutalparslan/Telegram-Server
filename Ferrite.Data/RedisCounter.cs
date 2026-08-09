// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using StackExchange.Redis;

namespace Ferrite.Data;

public class RedisCounter : IAtomicCounter
{
    private readonly ConnectionMultiplexer _redis;
    private readonly string _name;
    public RedisCounter(ConnectionMultiplexer redis, string name)
    {
        _redis = redis;
        _name = name;
    }

    public async ValueTask<long> Get()
    {
        object _asyncState = new object();
        IDatabase db = _redis.GetDatabase(asyncState: _asyncState);
        return (long)await db.StringGetAsync(_name);
    }

    public async ValueTask<long> IncrementAndGet()
    {
        object _asyncState = new object();
        IDatabase db = _redis.GetDatabase(asyncState: _asyncState);
        return await db.StringIncrementAsync(_name);
    }

    public async ValueTask<long> IncrementByAndGet(long inc)
    {
        object _asyncState = new object();
        IDatabase db = _redis.GetDatabase(asyncState: _asyncState);
        return await db.StringIncrementAsync(_name, inc);
    }

    public async ValueTask<long> IncrementTo(long value)
    {
        object _asyncState = new object();
        IDatabase db = _redis.GetDatabase(asyncState: _asyncState);
        while (true)
        {
            RedisValue current = await db.StringGetAsync(_name);
            if (current.HasValue && (long)current >= value)
            {
                return (long)current;
            }
            var tran = db.CreateTransaction();
            // Guard against a concurrent writer between the read and the set. A
            // missing key needs KeyNotExists; StringEqual(name, 0) would not match
            // an absent key and the set would silently never apply.
            tran.AddCondition(current.HasValue
                ? Condition.StringEqual(_name, current)
                : Condition.KeyNotExists(_name));
            _ = tran.StringSetAsync(_name, value);
            if (await tran.ExecuteAsync())
            {
                return value;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _redis.DisposeAsync();
    }
}

