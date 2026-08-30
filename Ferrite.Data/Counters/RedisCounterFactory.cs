// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using StackExchange.Redis;

namespace Ferrite.Data.Counters;

public class RedisCounterFactory : ICounterFactory
{
    private readonly ConnectionMultiplexer _redis;

    public RedisCounterFactory(string config)
    {
        _redis = ConnectionMultiplexer.Connect(config);
    }
    public IAtomicCounter GetCounter(string name)
    {
        return new RedisCounter(_redis, name);
    }
}