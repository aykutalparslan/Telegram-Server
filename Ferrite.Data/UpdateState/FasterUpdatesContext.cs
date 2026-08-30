// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Counters;
using Ferrite.Data.MessageBoxes;
using Ferrite.Data.Primitives;

namespace Ferrite.Data.UpdateState;

public class FasterUpdatesContext : IUpdatesContext
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<long, int>
        PendingPublications = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<long, int>
        Delivered = new();

    private static readonly TimeSpan PublicationWait = TimeSpan.FromSeconds(2);
    private readonly long? _authKeyId;
    private readonly long _userId;
    private readonly IAtomicCounter _counter;
    private readonly IMessageBox _commonMessageBox;
    private readonly ISecretMessageBox? _secondaryMessageBox;
    public FasterUpdatesContext(FasterContext<string, long> counterContext, 
        FasterContext<string, SortedSet<long>> unreadContext,
        FasterContext<string, SortedSet<string>> dialogContext,
        long? authKeyId, long userId)
    {
        _authKeyId = authKeyId;
        _userId = userId;
        _counter = new FasterCounter(counterContext,
            authKeyId != null ? $"seq:updates:auth:{authKeyId}" : $"seq:updates:{userId}");
        _commonMessageBox = new FasterMessageBox(counterContext, unreadContext, dialogContext, userId);
        _secondaryMessageBox = authKeyId != null ? new FasterSecretMessageBox(counterContext, (long)authKeyId) : null;
    }
    public async ValueTask<int> Pts()
    {
        return await _commonMessageBox.Pts();
    }

    public async ValueTask<int> IncrementPtsForMessage(int peerType, long peerId, int messageId)
    {
        return await _commonMessageBox.IncrementPtsForMessage(peerType, peerId, messageId);
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

    public ValueTask BeginPtsPublication()
    {
        PendingPublications.AddOrUpdate(_userId, 1, static (_, count) => count + 1);
        return ValueTask.CompletedTask;
    }

    public ValueTask CompletePtsPublication()
    {
        while (PendingPublications.TryGetValue(_userId, out int count))
        {
            if (count <= 1)
            {
                if (PendingPublications.TryRemove(
                        new KeyValuePair<long, int>(_userId, count)))
                {
                    break;
                }
            }
            else if (PendingPublications.TryUpdate(_userId, count - 1, count))
            {
                break;
            }
        }
        return ValueTask.CompletedTask;
    }

    public async ValueTask WaitForPtsPublications()
    {
        DateTime deadline = DateTime.UtcNow + PublicationWait;
        while (PendingPublications.TryGetValue(_userId, out int count) && count > 0)
        {
            if (DateTime.UtcNow >= deadline) return;
            await Task.Delay(1);
        }
    }

    public ValueTask<int> DeliveredPts() =>
        ValueTask.FromResult(Delivered.TryGetValue(_userId, out int pts) ? pts : 0);

    public ValueTask AdvanceDeliveredPts(int pts)
    {
        Delivered.AddOrUpdate(_userId, pts, (_, existing) => Math.Max(existing, pts));
        return ValueTask.CompletedTask;
    }

    public ValueTask<int> PendingPtsPublications()
    {
        return ValueTask.FromResult(PendingPublications.TryGetValue(_userId,
            out int count) ? count : 0);
    }
}
