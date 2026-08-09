// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer;
using Ferrite.Utils;

namespace Ferrite.Services;

public sealed record ScheduledMessageRunResult(int Flushed, int Refused,
    int Abandoned);

/// <summary>
/// The due coordinator for the scheduled-message queue: it flushes entries whose
/// send date has arrived, reconciles entries a previous process left mid-flush, and
/// flushes `when online` entries when their recipient comes back.
///
/// Every path goes through <see cref="ScheduledMessageFlusher"/>'s claim, so a timer
/// tick that overlaps a manual `messages.sendScheduledMessages` cannot send the same
/// entry twice.
/// </summary>
public sealed class ScheduledMessageRuntime
{
    /// <summary>
    /// How often the queue is scanned. A due entry is therefore sent within one tick
    /// of its date, which is the resolution Telegram's own queue advertises.
    /// </summary>
    public static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(5);

    private readonly ScheduledMessageStore _scheduled;
    private readonly ScheduledMessageFlusher _flusher;
    private readonly IUpdatesService _updates;
    private readonly ILogger _log;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _runGate = new(1, 1);
    private CancellationTokenSource? _stopping;
    private Task? _loop;

    public ScheduledMessageRuntime(ScheduledMessageStore scheduled,
        ScheduledMessageFlusher flusher, IUpdatesService updates, ILogger log)
    {
        _scheduled = scheduled;
        _flusher = flusher;
        _updates = updates;
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
            await ReconcileAsync(cancellationToken);
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
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            stopping?.Dispose();
        }
    }

    /// Flushes every entry whose send date has arrived.
    public async ValueTask<ScheduledMessageRunResult> RunOnceAsync(
        CancellationToken cancellationToken = default)
    {
        await _runGate.WaitAsync(cancellationToken);
        try
        {
            int now = _scheduled.UnixNow();
            IReadOnlyList<ScheduledMessageStore.ScheduledSnapshot> due =
                await _scheduled.GetDueAsync(now);
            return await FlushAllAsync(due, now, cancellationToken);
        }
        finally
        {
            _runGate.Release();
        }
    }

    /// <summary>
    /// Flushes the entries waiting for one user to come online. Called when that
    /// user reports themselves online, which is the only signal a
    /// `messageSchedulingStateSendWhenOnline` entry can ever have.
    /// </summary>
    public async ValueTask<ScheduledMessageRunResult> FlushWhenOnlineAsync(
        long recipientUserId, CancellationToken cancellationToken = default)
    {
        await _runGate.WaitAsync(cancellationToken);
        try
        {
            int now = _scheduled.UnixNow();
            IReadOnlyList<ScheduledMessageStore.ScheduledSnapshot> waiting =
                await _scheduled.GetWhenOnlineAsync(recipientUserId);
            return await FlushAllAsync(waiting, now, cancellationToken);
        }
        finally
        {
            _runGate.Release();
        }
    }

    /// <summary>
    /// Retires entries a previous process claimed and never finished. Whether that
    /// flush committed is not knowable from the row alone, so the entry is NOT
    /// re-sent: at-most-once is the contract, and a duplicate message is worse than
    /// a missed one. The owner is told the entry left the queue without real ids, so
    /// the loss is visible in the client rather than silent.
    /// </summary>
    public async ValueTask<int> ReconcileAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ScheduledMessageStore.ScheduledSnapshot> abandoned =
            await _scheduled.GetAbandonedClaimsAsync();
        foreach (ScheduledMessageStore.ScheduledSnapshot entry in abandoned)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _scheduled.Delete(entry);
            await _updates.EnqueueUpdate(entry.OwnerUserId,
                ScheduledMessageStore.BuildDeleteScheduledUpdate(entry.PeerType,
                    entry.PeerId, new[] { entry.ScheduledId }));
            _log.Warning($"⏰ Abandoning scheduled entry claimed by a previous run " +
                         $"user:{entry.OwnerUserId} " +
                         $"peer:{entry.PeerType}:{entry.PeerId} " +
                         $"scheduled:{entry.ScheduledId}; it is not re-sent because " +
                         $"the interrupted flush may already have delivered it.");
        }
        return abandoned.Count;
    }

    private async Task<ScheduledMessageRunResult> FlushAllAsync(
        IReadOnlyList<ScheduledMessageStore.ScheduledSnapshot> entries, int now,
        CancellationToken cancellationToken)
    {
        int flushed = 0;
        int refused = 0;
        foreach (ScheduledMessageStore.ScheduledSnapshot entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScheduledMessageFlusher.FlushOutcome outcome = await _flusher.FlushAsync(
                authKeyId: null, entry, now);
            if (outcome.Message is not { } message)
            {
                refused++;
                continue;
            }
            await _flusher.PublishAsync(entry, message);
            flushed++;
        }
        if (flushed > 0 || refused > 0)
        {
            _log.Debug($"⏰ Scheduled queue pass flushed:{flushed} refused:{refused}");
        }
        return new ScheduledMessageRunResult(flushed, refused, 0);
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
                        "Scheduled-message pass failed; the next tick will retry.");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
