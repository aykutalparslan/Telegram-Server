// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Counters;

namespace Ferrite.Data.MessageBoxes;

public class ChannelMessageBox : IChannelMessageBox
{
    private readonly IAtomicCounter _ptsCounter;
    private readonly IAtomicCounter _messageIdCounter;
    private readonly IAtomicCounter _pendingPublicationCounter;
    private bool _ptsSeeded;

    public ChannelMessageBox(ICounterFactory counterFactory, long channelId)
    {
        _ptsCounter = counterFactory.GetCounter($"counter_channel_pts_{channelId}");
        _messageIdCounter = counterFactory.GetCounter($"counter_channel_message_id_{channelId}");
        _pendingPublicationCounter = counterFactory.GetCounter(
            $"counter_channel_pending_publish_{channelId}");
    }

    public async ValueTask<int> Pts()
    {
        await EnsurePtsSeeded();
        return (int)await _ptsCounter.Get();
    }

    public async ValueTask<int> IncrementPts()
    {
        await EnsurePtsSeeded();
        return (int)await _ptsCounter.IncrementAndGet();
    }

    public async ValueTask<int> IncrementPts(int count)
    {
        await EnsurePtsSeeded();
        return (int)await _ptsCounter.IncrementByAndGet(count);
    }

    public async ValueTask<int> NextMessageId()
    {
        return (int)await _messageIdCounter.IncrementAndGet();
    }

    public async ValueTask BeginPtsPublication(int ptsCount = 1)
    {
        if (ptsCount > 0)
        {
            await _pendingPublicationCounter.IncrementByAndGet(ptsCount);
        }
    }

    public async ValueTask CompletePtsPublication(int ptsCount = 1)
    {
        if (ptsCount > 0)
        {
            await _pendingPublicationCounter.IncrementByAndGet(-ptsCount);
        }
    }

    public async ValueTask<int> PendingPtsPublications() =>
        Math.Max(0, (int)await _pendingPublicationCounter.Get());

    public async ValueTask<bool> WaitForPtsPublications(TimeSpan? timeout = null)
    {
        DateTime deadline = DateTime.UtcNow +
            (timeout ?? TimeSpan.FromSeconds(5));
        while (await PendingPtsPublications() > 0)
        {
            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }
            await Task.Delay(5);
        }
        return true;
    }

    private async ValueTask EnsurePtsSeeded()
    {
        if (_ptsSeeded) return;
        await _ptsCounter.IncrementTo(1);
        _ptsSeeded = true;
    }
}
