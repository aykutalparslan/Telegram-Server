// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Utils;

namespace Ferrite.Core.Connection;

public sealed class ReceivedMessageIdRegistry : IReceivedMessageIdRegistry
{
    public const int SessionCapacity = 4096;
    public const int MessageIdsPerSession = 64;

    private readonly object _lock = new();
    private readonly Dictionary<(long AuthKeyId, long SessionId), CircularQueue<long>>
        _bySession = new();
    private readonly Queue<(long AuthKeyId, long SessionId)> _order = new();

    public bool Contains(long authKeyId, long sessionId, long messageId)
    {
        lock (_lock)
        {
            return _bySession.TryGetValue((authKeyId, sessionId), out var received) &&
                   received.Contains(messageId);
        }
    }

    public void Add(long authKeyId, long sessionId, long messageId)
    {
        lock (_lock)
        {
            var key = (authKeyId, sessionId);
            if (!_bySession.TryGetValue(key, out var received))
            {
                while (_bySession.Count >= SessionCapacity &&
                       _order.TryDequeue(out var evicted))
                {
                    _bySession.Remove(evicted);
                }

                received = new CircularQueue<long>(MessageIdsPerSession);
                _bySession[key] = received;
                _order.Enqueue(key);
            }

            received.Enqueue(messageId);
        }
    }
}
