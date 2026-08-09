// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Threading.Channels;
using NonBlocking;

namespace Ferrite.Data;

public class LocalPipe : IMessagePipe
{
    private static readonly ConcurrentDictionary<string,
        ConcurrentDictionary<Guid, Channel<byte[]>>> Channels = new();
    private readonly Guid _subscriptionId = Guid.NewGuid();
    private readonly Channel<byte[]> _messages = Channel.CreateUnbounded<byte[]>();
    private string? _channel;
    
    public ValueTask<bool> SubscribeAsync(string channel)
    {
        if (_channel != null)
        {
            throw new InvalidOperationException("The pipe is already subscribed.");
        }
        _channel = channel;
        Channels.GetOrAdd(channel, _ => new())[ _subscriptionId ] = _messages;
        return ValueTask.FromResult(true);
    }

    public ValueTask<bool> UnSubscribeAsync()
    {
        if (_channel != null && Channels.TryGetValue(_channel, out var subscribers))
        {
            subscribers.TryRemove(_subscriptionId, out _);
            _channel = null;
        }
        return ValueTask.FromResult(true);
    }

    public async ValueTask<byte[]> ReadMessageAsync(CancellationToken cancellationToken = default)
    {
        if (_channel == null)
        {
            throw new InvalidOperationException("Subscribe must be called first.");
        }
        return await _messages.Reader.ReadAsync(cancellationToken);
    }

    public async ValueTask<bool> WriteMessageAsync(string channel, byte[] message)
    {
        if (!Channels.TryGetValue(channel, out var subscribers) ||
            subscribers.IsEmpty)
        {
            return false;
        }
        foreach (Channel<byte[]> subscriber in subscribers.Values)
        {
            await subscriber.Writer.WriteAsync(message);
        }
        return true;
    }
}
