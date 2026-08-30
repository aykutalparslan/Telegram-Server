// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using StackExchange.Redis;

namespace Ferrite.Data.UpdateState;

public class RedisUpdatesContextFactory : IUpdatesContextFactory
{
    private readonly ConnectionMultiplexer _redis;

    public RedisUpdatesContextFactory(string config)
    {
        _redis = ConnectionMultiplexer.Connect(config);
    }
    public IUpdatesContext GetUpdatesContext(long? authKeyId, long userId)
    {
        return new RedisUpdatesContext(_redis, authKeyId, userId);
    }
}