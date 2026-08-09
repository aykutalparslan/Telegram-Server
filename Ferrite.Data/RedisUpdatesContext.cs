// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using StackExchange.Redis;

namespace Ferrite.Data;

public class RedisUpdatesContext : IUpdatesContext
{
    private readonly ConnectionMultiplexer _redis;
    private readonly long? _authKeyId;
    private readonly long _userId;
    private readonly IAtomicCounter _counter;
    private readonly IMessageBox _commonMessageBox;
    private readonly ISecretMessageBox? _secondaryMessageBox;
    public RedisUpdatesContext(ConnectionMultiplexer redis, long? authKeyId, long userId)
    {
        _redis = redis;
        _authKeyId = authKeyId;
        _userId = userId;
        // Updates `seq` is per-session state; see FasterUpdatesContext.
        _counter = new RedisCounter(redis,
            authKeyId != null ? $"seq:updates:auth:{authKeyId}" : $"seq:updates:{userId}");
        _commonMessageBox = new RedisMessageBox(redis, userId);
        _secondaryMessageBox = authKeyId != null ? new RedisSecretMessageBox(redis, (long)authKeyId) : null;
    }
    public async ValueTask<int> Pts()
    {
        return await _commonMessageBox.Pts();
    }

    public async ValueTask<int> IncrementPtsForMessage(int peerType, long peerId, int messageId)
    {
        return await _commonMessageBox.IncrementPtsForMessage(peerType,peerId, messageId);
    }

    public async ValueTask<int> NextMessageId()
    {
        return await _commonMessageBox.NextMessageId();
    }

    public async ValueTask<int> ReadMessages(int peerType, long peerId, int maxId)
    {
        return await _commonMessageBox.ReadMessages(peerType, peerId, maxId);
    }

    public async ValueTask<int> ReadMessagesMaxId(int peerType, long peerId)
    {
        return await _commonMessageBox.ReadMessagesMaxId(peerType, peerId);
    }

    public async ValueTask<int> UnreadMessages()
    {
        return await _commonMessageBox.UnreadMessages();
    }

    public async ValueTask<int> UnreadMessages(int peerType, long peerId)
    {
        return await _commonMessageBox.UnreadMessages(peerType, peerId);
    }

    public async ValueTask<int> IncrementPts()
    {
        return await _commonMessageBox.IncrementPts();
    }

    public async ValueTask<int> IncrementPts(int count)
    {
        return await _commonMessageBox.IncrementPts(count);
    }

    public async ValueTask<int> Qts()
    {
        return _secondaryMessageBox != null ? await _secondaryMessageBox.Qts() : 0;
    }

    public async ValueTask<int> IncrementQts()
    {
        return _secondaryMessageBox != null ? await _secondaryMessageBox.IncrementQts() : 0;
    }

    public async Task<int> Seq()
    {
        return (int)await _counter.Get();
    }

    public async Task<int> IncrementSeq()
    {
        return (int)await _counter.IncrementAndGet();
    }

    public async ValueTask BeginPtsPublication()
    {
        IDatabase db = _redis.GetDatabase();
        RedisKey key = $"updates:pending-publish:{_userId}";
        await db.StringIncrementAsync(key);
        await db.KeyExpireAsync(key, TimeSpan.FromSeconds(30));
    }

    public async ValueTask CompletePtsPublication()
    {
        IDatabase db = _redis.GetDatabase();
        RedisKey key = $"updates:pending-publish:{_userId}";
        long remaining = await db.StringDecrementAsync(key);
        if (remaining <= 0)
        {
            await db.KeyDeleteAsync(key);
        }
    }

    public async ValueTask WaitForPtsPublications()
    {
        IDatabase db = _redis.GetDatabase();
        RedisKey key = $"updates:pending-publish:{_userId}";
        while ((long)await db.StringGetAsync(key) > 0)
        {
            await Task.Delay(5);
        }
    }

    public async ValueTask<int> PendingPtsPublications()
    {
        RedisValue value = await _redis.GetDatabase().StringGetAsync(
            $"updates:pending-publish:{_userId}");
        return value.HasValue ? (int)(long)value : 0;
    }
}
