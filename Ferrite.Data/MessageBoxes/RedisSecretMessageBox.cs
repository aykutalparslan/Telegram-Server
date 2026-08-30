// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using StackExchange.Redis;
using Ferrite.Data.Counters;

namespace Ferrite.Data.MessageBoxes;

public class RedisSecretMessageBox : ISecretMessageBox
{
    private readonly ConnectionMultiplexer _redis;
    private readonly IAtomicCounter _counter;
    private readonly long _authKeyId;
    public RedisSecretMessageBox(ConnectionMultiplexer redis, long authKeyId)
    {
        _redis = redis;
        _authKeyId = authKeyId;
        _counter = new RedisCounter(redis, $"seq:qts:{authKeyId}");
    }
    public async ValueTask<int> Qts()
    {
        return (int)await _counter.IncrementTo(1);
    }

    public async ValueTask<int> IncrementQts()
    {
        await _counter.IncrementTo(1);
        return (int)await _counter.IncrementAndGet();
    }
}
