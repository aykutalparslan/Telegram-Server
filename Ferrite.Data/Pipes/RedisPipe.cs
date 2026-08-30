// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC
using System;
using System.Threading.Tasks.Sources;
using StackExchange.Redis;

namespace Ferrite.Data.Pipes;

public sealed class RedisPipe : IMessagePipe, IAsyncDisposable
{
    private readonly ConnectionMultiplexer _redis;
    private ChannelMessageQueue? _messageQueue;
    public RedisPipe(string config)
    {
        _redis = ConnectionMultiplexer.Connect(config);
    }

    public async ValueTask<byte[]> ReadMessageAsync(CancellationToken cancellationToken=default)
    {
        if (_messageQueue == null)
        {
            throw new InvalidOperationException("Subscribe must be called first.");
        }
        var message = await _messageQueue.ReadAsync(cancellationToken);
        return (byte[])message.Message;
    }

    public async ValueTask<bool> SubscribeAsync(string channel)
    {
        Interlocked.CompareExchange(ref _messageQueue,
            await _redis.GetSubscriber().SubscribeAsync(channel),
            null);
        return true;
    }

    public async ValueTask<bool> UnSubscribeAsync()
    {
        if (_messageQueue == null)
        {
            throw new InvalidOperationException("Not subscribed.");
        }
        await _messageQueue.UnsubscribeAsync();
        _messageQueue = null;
        return true;
    }

    public async ValueTask<bool> WriteMessageAsync(string channel, byte[] message)
    {
        object _asyncState = new object();
        IDatabase db = _redis.GetDatabase(asyncState: _asyncState);
        _ = await db.PublishAsync((RedisChannel)channel, (RedisValue)message);
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_messageQueue != null)
        {
            await _messageQueue.UnsubscribeAsync();
            _messageQueue = null;
        }
        await _redis.DisposeAsync();
    }
}
