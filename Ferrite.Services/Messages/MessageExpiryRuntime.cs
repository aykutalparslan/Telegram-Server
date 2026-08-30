// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Messages;

public sealed record MessageExpiryRunResult(int Deleted, int Retired);

public sealed class MessageExpiryRuntime
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IMessageRepository _messageRepository;

    public static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(5);

    private readonly IUnitOfWork _unitOfWork;
    private readonly MessageExpiryStore _expiry;
    private readonly UpdateFanout _fanout;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly ICounterFactory _counterFactory;
    private readonly ILogger _log;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _runGate = new(1, 1);
    private CancellationTokenSource? _stopping;
    private Task? _loop;

    public MessageExpiryRuntime(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IMessageRepository messageRepository, MessageExpiryStore expiry,
        UpdateFanout fanout, IUpdatesContextFactory updatesContextFactory,
        ICounterFactory counterFactory, ILogger log)
    {
        _channelMessagesRepository = channelMessagesRepository;
        _messageRepository = messageRepository;

        _unitOfWork = unitOfWork;
        _expiry = expiry;
        _fanout = fanout;
        _updatesContextFactory = updatesContextFactory;
        _counterFactory = counterFactory;
        _log = log;
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (_loop is not null)
            {
                return;
            }
            await RunOnceAsync(cancellationToken);
            _stopping = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            _loop = RunLoopAsync(_stopping.Token);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        Task? loop;
        CancellationTokenSource? stopping;
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (_loop is null)
            {
                return;
            }
            _stopping!.Cancel();
            loop = _loop;
            stopping = _stopping;
            _loop = null;
            _stopping = null;
        }
        finally
        {
            _lifecycleGate.Release();
        }

        try
        {
            await loop.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            stopping?.Dispose();
        }
    }

    public async ValueTask<MessageExpiryRunResult> RunOnceAsync(
        CancellationToken cancellationToken = default)
    {
        await _runGate.WaitAsync(cancellationToken);
        try
        {
            int now = _expiry.UnixNow();
            IReadOnlyList<MessageExpiryStore.ExpirySnapshot> due =
                await _expiry.GetDueAsync(now);
            if (due.Count == 0)
            {
                return new MessageExpiryRunResult(0, 0);
            }

            var batches = new List<(int BoxType, long BoxId, List<int> Ids,
                int Pts)>();
            int retired = 0;
            foreach (IGrouping<(int BoxType, long BoxId),
                         MessageExpiryStore.ExpirySnapshot> box in due.GroupBy(
                         static entry => (entry.BoxType, entry.BoxId)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                (List<int> ids, int boxRetired) = box.Key.BoxType ==
                                                  MessageExpiryBox.Channel
                    ? await RemoveChannelCopiesAsync(box.Key.BoxId, box)
                    : await RemoveCommonCopiesAsync(box.Key.BoxId, box);
                retired += boxRetired;
                if (ids.Count > 0)
                {
                    int pts = 0;
                    if (box.Key.BoxType == MessageExpiryBox.Channel)
                    {
                        var channelBox = new ChannelMessageBox(_counterFactory,
                            box.Key.BoxId);
                        pts = await channelBox.IncrementPts(ids.Count);
                        _fanout.PersistDeleteChannelMessages(box.Key.BoxId, ids,
                            pts, ids.Count);
                    }
                    batches.Add((box.Key.BoxType, box.Key.BoxId, ids, pts));
                }
            }

            await _unitOfWork.SaveAsync();

            int deleted = 0;
            foreach ((int boxType, long boxId, List<int> ids, int pts) in batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (boxType == MessageExpiryBox.Channel)
                {
                    await ReportChannelDeletionAsync(boxId, ids, pts);
                }
                else
                {
                    await ReportCommonDeletionAsync(boxId, ids);
                }
                deleted += ids.Count;
            }

            if (deleted > 0 || retired > 0)
            {
                _log.Debug($"⌛ Expiry pass deleted:{deleted} retired:{retired}");
            }
            return new MessageExpiryRunResult(deleted, retired);
        }
        finally
        {
            _runGate.Release();
        }
    }

    private async Task<(List<int> Ids, int Retired)> RemoveCommonCopiesAsync(
        long ownerId, IEnumerable<MessageExpiryStore.ExpirySnapshot> entries)
    {
        var deletedIds = new List<int>();
        int retired = 0;
        foreach (MessageExpiryStore.ExpirySnapshot entry in entries)
        {
            using TLSavedMessage? stored = await _messageRepository
                .GetMessageAsync(ownerId, entry.MessageId);
            _expiry.Untrack(entry.BoxType, entry.BoxId, entry.MessageId);
            if (stored == null)
            {
                retired++;
                continue;
            }
            _messageRepository.DeleteMessage(ownerId, entry.MessageId);
            deletedIds.Add(entry.MessageId);
        }
        return (deletedIds, retired);
    }

    private async Task<(List<int> Ids, int Retired)> RemoveChannelCopiesAsync(
        long channelId, IEnumerable<MessageExpiryStore.ExpirySnapshot> entries)
    {
        var deletedIds = new List<int>();
        int retired = 0;
        foreach (MessageExpiryStore.ExpirySnapshot entry in entries)
        {
            using TLSavedMessage? stored = await _channelMessagesRepository.GetMessageAsync(channelId,
                    entry.MessageId);
            _expiry.Untrack(entry.BoxType, entry.BoxId, entry.MessageId);
            if (stored == null)
            {
                retired++;
                continue;
            }
            await _channelMessagesRepository.DeleteMessageAsync(channelId,
                entry.MessageId);
            deletedIds.Add(entry.MessageId);
        }
        return (deletedIds, retired);
    }

    private async Task ReportCommonDeletionAsync(long ownerId,
        IReadOnlyList<int> deletedIds)
    {
        IUpdatesContext context = _updatesContextFactory.GetUpdatesContext(null,
            ownerId);
        int pts = await _fanout.AdvanceAndEnqueueDeleteMessagesAsync(ownerId,
            deletedIds, context);
        _log.Debug($"⌛ Auto-deleted user:{ownerId} count:{deletedIds.Count} " +
                   $"pts:{pts}");
    }

    private async Task ReportChannelDeletionAsync(long channelId,
        IReadOnlyList<int> deletedIds, int pts)
    {
        await _fanout.DeliverDeleteChannelMessagesAsync(channelId, actorUserId: 0,
            deletedIds, pts, deletedIds.Count);
        _log.Debug($"⌛ Auto-deleted channel:{channelId} " +
                   $"count:{deletedIds.Count} pts:{pts}");
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(ScanInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await RunOnceAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (
                    cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _log.Warning(exception,
                        "Message-expiry pass failed; the next tick will retry.");
                }
            }
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
        }
    }
}
