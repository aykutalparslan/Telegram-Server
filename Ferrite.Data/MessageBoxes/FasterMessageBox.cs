// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Counters;
using Ferrite.Data.Primitives;

namespace Ferrite.Data.MessageBoxes;

public class FasterMessageBox : IMessageBox, IAsyncDisposable
{
    private readonly IAtomicCounter _ptsCounter;
    private bool _ptsSeeded;
    private readonly IAtomicCounter _messageIdCounter;
    private readonly IAtomicCounter _maxIdCounter;
    private readonly long _userId;
    private readonly FasterContext<string, SortedSet<long>> _unreadContext;
    private readonly FasterSortedSet<string> _dialogs;
    private readonly FasterContext<string, long> _counterContext;

    public FasterMessageBox(FasterContext<string, long> counterContext, 
        FasterContext<string, SortedSet<long>> unreadContext,
        FasterContext<string, SortedSet<string>> dialogContext,
        long userId)
    {
        _counterContext = counterContext;
        _unreadContext = unreadContext;
        _userId = userId;
        _dialogs = new FasterSortedSet<string>(dialogContext, $"msg:dialogs:{userId}");
        _ptsCounter = new FasterCounter(counterContext , $"seq:pts:{userId}");
        _messageIdCounter = new FasterCounter(counterContext , $"seq:message:id:{userId}");
    }
    public async ValueTask<int> Pts()
    {
        await EnsurePtsSeeded();
        return (int)await _ptsCounter.Get();
    }

    public async ValueTask<int> IncrementPtsForMessage(int peerType, long peerId, int messageId)
    {
        FasterSortedSet<long> unreadForPeer = new FasterSortedSet<long>(_unreadContext,
            $"msg:unread:{_userId}-{peerType}-{peerId}");
        _dialogs.Add($"msg:unread:{_userId}-{peerType}-{peerId}");
        unreadForPeer.Add(messageId);
        return await IncrementPtsCounter();
    }

    public async ValueTask<int> NextMessageId()
    {
        return (int)await _messageIdCounter.IncrementAndGet();
    }

    public async ValueTask<int> ReadMessages(int peerType, long peerId, int maxId)
    {
        FasterSortedSet<long> unreadForPeer = new FasterSortedSet<long>(_unreadContext,
            $"msg:unread:{_userId}-{peerType}-{peerId}");
        await unreadForPeer.RemoveEqualOrLess(maxId);
        if (unreadForPeer.Get().Count == 0)
        {
            await _dialogs.Remove($"msg:unread:{_userId}-{peerType}-{peerId}");
        }
        var peerMaxReadCounter = new FasterCounter(_counterContext , 
            $"msg:max-read:{_userId}-{peerType}-{peerId}");
        return (int)await peerMaxReadCounter.IncrementTo(maxId);
    }

    public async ValueTask<int> ReadMessagesMaxId(int peerType, long peerId)
    {
        var peerMaxReadCounter = new FasterCounter(_counterContext , 
            $"msg:max-read:{_userId}-{peerType}-{peerId}");
        return (int)await peerMaxReadCounter.Get();
    }

    public ValueTask<int> UnreadMessages()
    {
        int unread = 0;
        var dialogs = _dialogs.Get();
        if(dialogs == null) return ValueTask.FromResult(unread);
        foreach (var d in dialogs)
        {
            FasterSortedSet<long> unreadForPeer = new FasterSortedSet<long>(_unreadContext, d);
            unread += unreadForPeer.Get().Count;
        }

        return ValueTask.FromResult(unread);
    }

    public ValueTask<int> UnreadMessages(int peerType, long peerId)
    {
        FasterSortedSet<long> unreadForPeer = new FasterSortedSet<long>(_unreadContext, 
            $"msg:unread:{_userId}-{peerType}-{peerId}");
        return ValueTask.FromResult(unreadForPeer.Get().Count);
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

    public async ValueTask DisposeAsync()
    {
        await _unreadContext.DisposeAsync();
        await _dialogs.DisposeAsync();
        await _counterContext.DisposeAsync();
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
