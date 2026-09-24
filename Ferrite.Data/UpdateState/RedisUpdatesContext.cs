// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using StackExchange.Redis;
using Ferrite.Data.Counters;
using Ferrite.Data.MessageBoxes;

namespace Ferrite.Data.UpdateState;

public class RedisUpdatesContext : IUpdatesContext
{
    private readonly ConnectionMultiplexer _redis;
    private readonly long? _authKeyId;
    private readonly long _userId;
    private readonly IAtomicCounter _counter;
    private readonly IMessageBox _commonMessageBox;
    private readonly ISecretMessageBox? _secondaryMessageBox;
    private static readonly TimeSpan PublicationWait = TimeSpan.FromSeconds(2);
    private const string AdvanceDeliveredScript =
        "local current = tonumber(redis.call('GET', KEYS[1]) or '0') " +
        "if tonumber(ARGV[1]) > current then " +
        "redis.call('SET', KEYS[1], ARGV[1]) end " +
        "return redis.call('GET', KEYS[1])";
    private const string SettlePtsScript = """
        local committed = tonumber(redis.call('GET', KEYS[2]) or '1')
        local first, last = tonumber(ARGV[1]), tonumber(ARGV[2])
        if last <= committed then return 0 end
        local previous = redis.call('ZSCORE', KEYS[1], ARGV[2])
        if not previous or first < tonumber(previous) then
            redis.call('ZADD', KEYS[1], first, ARGV[2])
        end
        return 1
        """;
    private const string ExtendCommittedPtsScript = """
        local committed = math.max(tonumber(ARGV[1]), tonumber(redis.call('GET', KEYS[2]) or '1'))
        local ranges = redis.call('ZRANGE', KEYS[1], 0, -1, 'WITHSCORES')
        for i = 1, #ranges, 2 do
            if tonumber(ranges[i + 1]) > committed + 1 then break end
            committed = math.max(committed, tonumber(ranges[i]))
            redis.call('ZREM', KEYS[1], ranges[i])
        end
        redis.call('SET', KEYS[2], committed)
        return committed
        """;
    public RedisUpdatesContext(ConnectionMultiplexer redis, long? authKeyId, long userId)
    {
        _redis = redis;
        _authKeyId = authKeyId;
        _userId = userId;
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
        int pts = await _commonMessageBox.IncrementPts();
        await RecordSessionDelivery(pts);
        return pts;
    }

    public async ValueTask<int> IncrementPts(int count)
    {
        int pts = await _commonMessageBox.IncrementPts(count);
        await RecordSessionDelivery(pts);
        return pts;
    }

    private ValueTask RecordSessionDelivery(int pts) =>
        _authKeyId != null ? AdvanceDeliveredPts(pts) : ValueTask.CompletedTask;

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
        DateTime deadline = DateTime.UtcNow + PublicationWait;
        while ((long)await db.StringGetAsync(key) > 0)
        {
            if (DateTime.UtcNow >= deadline) return;
            await Task.Delay(5);
        }
    }

    public async ValueTask<int> DeliveredPts()
    {
        RedisValue value = await _redis.GetDatabase().StringGetAsync(
            $"updates:delivered-pts:{_userId}");
        return value.HasValue ? (int)value : 0;
    }

    public async ValueTask AdvanceDeliveredPts(int pts)
    {
        await _redis.GetDatabase().ScriptEvaluateAsync(AdvanceDeliveredScript,
            new RedisKey[] { $"updates:delivered-pts:{_userId}" },
            new RedisValue[] { pts });
    }

    public async ValueTask<int> PendingPtsPublications()
    {
        RedisValue value = await _redis.GetDatabase().StringGetAsync(
            $"updates:pending-publish:{_userId}");
        return value.HasValue ? (int)(long)value : 0;
    }

    public async ValueTask SettlePts(int first, int last)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(first, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(last, first);
        await _redis.GetDatabase().ScriptEvaluateAsync(SettlePtsScript,
            new RedisKey[] { $"updates:settled-pts:{_userId}", $"updates:committed-pts:{_userId}" },
            new RedisValue[] { first, last });
    }

    public async ValueTask<int> ExtendCommittedPts(int committed)
    {
        return (int)await _redis.GetDatabase().ScriptEvaluateAsync(ExtendCommittedPtsScript,
            new RedisKey[] { $"updates:settled-pts:{_userId}", $"updates:committed-pts:{_userId}" },
            new RedisValue[] { committed });
    }
}
