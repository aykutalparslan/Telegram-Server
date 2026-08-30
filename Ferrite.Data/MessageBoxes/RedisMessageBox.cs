// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using StackExchange.Redis;
using Ferrite.Data.Counters;

namespace Ferrite.Data.MessageBoxes;

public class RedisMessageBox : IMessageBox
{
    private readonly ConnectionMultiplexer _redis;
    private readonly IAtomicCounter _ptsCounter;
    private bool _ptsSeeded;
    private readonly IAtomicCounter _messageIdCounter;
    private readonly long _userId;
    public RedisMessageBox(ConnectionMultiplexer redis, long userId)
    {
        _redis = redis;
        _userId = userId;
        _ptsCounter = new RedisCounter(redis, $"seq:pts:{userId}");
        _messageIdCounter = new RedisCounter(redis, $"seq:message:id:{userId}");
    }

    public async ValueTask<int> Pts()
    {
        await EnsurePtsSeeded();
        return (int)await _ptsCounter.Get();
    }

    public async ValueTask<int> IncrementPtsForMessage(int peerType, long peerId, int messageId)
    {
        IDatabase db = _redis.GetDatabase();
        RedisKey key = $"msg:unread:{_userId}-{peerType}-{peerId}";
        RedisKey dialogsKey = $"msg:dialogs:{_userId}";
        db.SortedSetAdd(dialogsKey, $"msg:unread:{_userId}-{peerType}-{peerId}", 0);
        db.SortedSetAdd(key, messageId, messageId);
        return await IncrementPtsCounter();
    }

    public async ValueTask<int> NextMessageId()
    {
        return (int)await _messageIdCounter.IncrementAndGet();
    }

    public async ValueTask<int> ReadMessages(int peerType, long peerId, int maxId)
    {
        IDatabase db = _redis.GetDatabase();
        RedisKey key = $"msg:unread:{_userId}-{peerType}-{peerId}";
        await db.SortedSetRemoveRangeByScoreAsync(key, 0, maxId);
        GetUnread(db, out var unread);
        RedisKey keyRead = $"msg:max-read:{_userId}-{peerType}-{peerId}";
        bool success = false;
        while (!success)
        {
            RedisValue current = await db.StringGetAsync(keyRead);
            int oldValue = current.HasValue ? (int)current : 0;
            if (oldValue > maxId)
            {
                break;
            }
            var tran = db.CreateTransaction();
            tran.AddCondition(current.HasValue
                ? Condition.StringEqual(keyRead, current)
                : Condition.KeyNotExists(keyRead));
            _ = tran.StringSetAsync(keyRead, maxId);
            success = await tran.ExecuteAsync();
        }
        
        return unread;
    }

    private void GetUnread(IDatabase db, out int unread)
    {
        unread = 0;
        RedisKey dialogsKey = $"msg:dialogs:{_userId}";
        var dialogs = db.SortedSetScan(dialogsKey);
        foreach (var e in dialogs)
        {
            unread += (int) db.SortedSetLength(new RedisKey((string)e.Element));
        }
    }

    public async ValueTask<int> ReadMessagesMaxId(int peerType, long peerId)
    {
        IDatabase db = _redis.GetDatabase();
        RedisKey keyRead = $"msg:max-read:{_userId}-{peerType}-{peerId}";
        return (int)await db.StringGetAsync(keyRead);
    }

    public async ValueTask<int> UnreadMessages()
    {
        IDatabase db = _redis.GetDatabase();
        GetUnread(db, out var unread);

        return unread;
    }

    public async ValueTask<int> UnreadMessages(int peerType, long peerId)
    {
        IDatabase db = _redis.GetDatabase();
        RedisKey key = $"msg:unread:{_userId}-{peerType}-{peerId}";
        return (int)await db.SortedSetLengthAsync(key);
    }

    public async ValueTask<int> IncrementPts()
    {
        return await IncrementPtsCounter();
    }

    public async ValueTask<int> IncrementPts(int count)
    {
        await EnsurePtsSeeded();
        return (int)await _ptsCounter.IncrementByAndGet(count);
    }

    private async ValueTask<int> IncrementPtsCounter()
    {
        await EnsurePtsSeeded();
        return (int)await _ptsCounter.IncrementAndGet();
    }

    private async ValueTask EnsurePtsSeeded()
    {
        if (_ptsSeeded) return;
        await _ptsCounter.IncrementTo(1);
        _ptsSeeded = true;
    }
}
