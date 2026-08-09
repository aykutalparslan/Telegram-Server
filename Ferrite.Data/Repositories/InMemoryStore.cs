// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Threading.Channels;
using NonBlocking;

namespace Ferrite.Data.Repositories;

public class InMemoryStore : IVolatileKVStore
{
    // We will be using Redis unless we have a really small deployment
    // in which case this will just do fine however we should still
    // optimize this in the future
    // TODO: Benchmark and optimize this
    private readonly ConcurrentDictionary<byte[], (byte[], long)> _dictionary = new(new ArrayEqualityComparer());
    private readonly ConcurrentDictionary<byte[], (SortedList<long, byte[]>, long)> _lists =
        new(new ArrayEqualityComparer());
    private readonly object _listLock = new();
    private readonly PriorityQueue<MemcomparableKey, long> _ttlQueue = new PriorityQueue<MemcomparableKey, long>();
    private readonly Channel<MemcomparableKey> _ttlChannel = Channel.CreateUnbounded<MemcomparableKey>();
    private readonly Task? _expire;
    private readonly Task? _addTtl;
    private TableDefinition? _table;

    public InMemoryStore()
    {
        _expire = DoExpire();
        _addTtl = DoAddTtl();
    }

    private async Task DoExpire()
    {
        (byte[], long) current;
        while (true)
        {
            await Task.Delay(100);
            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (!_ttlQueue.TryPeek(out var currentKey, out var expiration) ||
                expiration > now)
            {
                continue;
            }
            
            while (expiration <= now && _ttlQueue.Count > 0)
            {
                _ttlQueue.TryDequeue(out currentKey, out var currentPriority);
                if (currentPriority <= now)
                {
                    if (_dictionary.TryGetValue(currentKey.ArrayValue, out current) &&
                        current.Item2 <= now)
                    {
                        _dictionary.TryRemove(currentKey.ArrayValue, out current);
                    }
                    if (_lists.TryGetValue(currentKey.ArrayValue, out var list) &&
                        list.Item2 <= now)
                    {
                        _lists.TryRemove(currentKey.ArrayValue, out _);
                    }
                }
                else
                {
                    _ttlQueue.Enqueue(currentKey, currentPriority);
                }
                if (!_ttlQueue.TryPeek(out currentKey, out expiration))
                {
                    expiration = long.MaxValue;
                }
            }
        }
    }
    
    private async Task DoAddTtl()
    {
        while (true)
        {
            var key = await _ttlChannel.Reader.ReadAsync();
            _ttlQueue.Enqueue(key, key.ExpiresAt);
        }
    }

    public void SetSchema(TableDefinition table)
    {
        _table = table;
    }
    public void Put(byte[] value, TimeSpan? ttl = null, params object[] keys)
    {
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        if (ttl.HasValue)
        {
            primaryKey.ExpiresAt = DateTimeOffset.Now.ToUnixTimeMilliseconds() + (long)ttl.Value.TotalMilliseconds;
        }
        _dictionary[primaryKey.ArrayValue] = (value, primaryKey.ExpiresAt);
        _lists.TryRemove(primaryKey.ArrayValue, out _);
        if (ttl.HasValue)
        {
            _ttlChannel.Writer.WriteAsync(primaryKey);
        }
    }

    public void UpdateTtl(TimeSpan? ttl = null, params object[] keys)
    {
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        if (ttl.HasValue)
        {
            primaryKey.ExpiresAt = DateTimeOffset.Now.ToUnixTimeMilliseconds() + (long)ttl.Value.TotalMilliseconds;
        }
        if (_dictionary.ContainsKey(primaryKey.ArrayValue))
        {
            _dictionary.TryGetValue(primaryKey.ArrayValue, out var current);
            _dictionary.TryUpdate(primaryKey.ArrayValue, (current.Item1, primaryKey.ExpiresAt), current);
            if (ttl.HasValue)
            {
                _ttlChannel.Writer.WriteAsync(primaryKey);
            }
        }
        if (_lists.TryGetValue(primaryKey.ArrayValue, out var list))
        {
            _lists.TryUpdate(primaryKey.ArrayValue,
                (list.Item1, primaryKey.ExpiresAt), list);
            if (ttl.HasValue) _ttlChannel.Writer.TryWrite(primaryKey);
        }
    }

    public bool ListAdd(long score, byte[] value, TimeSpan? ttl = null, params object[] keys)
    {
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        long expiresAt = 0;
        if (ttl.HasValue)
        {
            expiresAt = DateTimeOffset.Now.ToUnixTimeMilliseconds() + (long)ttl.Value.TotalMilliseconds;
        }
        long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        lock (_listLock)
        {
            if (!_lists.TryGetValue(primaryKey.ArrayValue, out var existing) ||
                existing.Item2 > 0 && existing.Item2 <= now)
            {
                existing = (new SortedList<long, byte[]>(), 0);
            }
            var list = existing.Item1;
            if (ttl.HasValue)
            {
                foreach (long existingScore in list.Keys)
                    expiresAt = Math.Max(existingScore, expiresAt);
            }
            while (list.ContainsKey(score)) score++;
            list.Add(score, value);
            primaryKey.ExpiresAt = expiresAt;
            _lists[primaryKey.ArrayValue] = (list, expiresAt);
            _dictionary.TryRemove(primaryKey.ArrayValue, out _);
            if (ttl.HasValue) _ttlChannel.Writer.TryWrite(primaryKey);
        }
        return true;
    }

    public bool ListDelete(byte[] value, params object[] keys)
    {
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        lock (_listLock)
        {
            if (!_lists.TryGetValue(primaryKey.ArrayValue, out var existing)) return true;
            if (existing.Item2 > 0 && existing.Item2 <= now)
            {
                _lists.TryRemove(primaryKey.ArrayValue, out _);
                return true;
            }
            List<long> toBeRemoved = [];
            foreach ((long key, byte[] entry) in existing.Item1)
            {
                if (entry.SequenceEqual(value)) toBeRemoved.Add(key);
            }
            foreach (long key in toBeRemoved) existing.Item1.Remove(key);
        }
        return true;
    }

    public bool ListDeleteByScore(long score, params object[] keys)
    {
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        lock (_listLock)
        {
            if (!_lists.TryGetValue(primaryKey.ArrayValue, out var existing)) return true;
            if (existing.Item2 > 0 && existing.Item2 <= now)
            {
                _lists.TryRemove(primaryKey.ArrayValue, out _);
                return true;
            }
            List<long> toBeRemoved = [];
            foreach (long key in existing.Item1.Keys)
            {
                if (key > score) break;
                toBeRemoved.Add(key);
            }
            foreach (long key in toBeRemoved) existing.Item1.Remove(key);
        }
        return true;
    }

    public IList<byte[]> ListGet(params object[] keys)
    {
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        // Reads must not consume the list; only an expired entry is removed. Redis
        // sorted-set reads are non-destructive and this store mirrors them.
        bool found = _lists.TryGetValue(primaryKey.ArrayValue, out var existing);
        long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        if (found && (existing.Item2 <= 0 || existing.Item2 > now))
        {
            lock (_listLock)
            {
                return existing.Item1.Values.ToList();
            }
        }
        if (found)
        {
            _lists.TryRemove(primaryKey.ArrayValue, out _);
        }
        return Array.Empty<byte[]>();
    }

    public void Delete(params object[] keys)
    {
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        _dictionary.TryRemove(primaryKey.ArrayValue, out var removed);
        _lists.TryRemove(primaryKey.ArrayValue, out _);
    }

    public bool Exists(params object[] keys)
    {
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        
        if (!_dictionary.TryGetValue(primaryKey.ArrayValue, out var value))
        {
            if (!_lists.TryGetValue(primaryKey.ArrayValue, out var list)) return false;
            long listNow = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (list.Item2 > 0 && list.Item2 <= listNow)
            {
                _lists.TryRemove(primaryKey.ArrayValue, out _);
                return false;
            }
            return true;
        }
        long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        if (value.Item2 > 0 && value.Item2 <= now)
        {
            _dictionary.TryRemove(primaryKey.ArrayValue, out var removed);
            return false;
        }

        return true;
    }

    public byte[]? Get(params object[] keys)
    {
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        _dictionary.TryGetValue(primaryKey.ArrayValue, out var value);
        long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        if (value.Item2 > 0 && value.Item2 <= now)
        {
            _dictionary.TryRemove(primaryKey.ArrayValue, out var removed);
            return null;
        }
        return value.Item1;
    }

    public ValueTask<byte[]?> GetAsync(params object[] keys)
    {
        var primaryKey = MemcomparableKey.Create(_table.FullName, keys);
        _dictionary.TryGetValue(primaryKey.ArrayValue, out var value);
        long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        if (value.Item2 > 0 && value.Item2 <= now)
        {
            _dictionary.TryRemove(primaryKey.ArrayValue, out var removed);
            return ValueTask.FromResult<byte[]?>(null);
        }
        return ValueTask.FromResult<byte[]?>(value.Item1);;
    }
}
