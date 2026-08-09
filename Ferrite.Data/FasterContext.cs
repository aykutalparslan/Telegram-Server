// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using FASTER.core;

namespace Ferrite.Data;

public class FasterContext<TKey, TValue> : IAsyncDisposable
{
    public FasterKV<TKey, TValue> Store { get; }
    private bool _disposed = false;
    private readonly Task? _checkpointHybrid;
    private readonly Task? _checkpointFull;
    
    public FasterContext()
    {
        Store = new FasterKV<TKey, TValue>(new FasterKVSettings<TKey, TValue>(null)
        {
            TryRecoverLatest = true,
            RemoveOutdatedCheckpoints = true,
        });
        _checkpointHybrid = IssueHybridLogCheckpoints();
        _checkpointFull = IssueFullCheckpoints();
    }
    public FasterContext(string path)
    {
        Store = new FasterKV<TKey, TValue>(new FasterKVSettings<TKey, TValue>(path, deleteDirOnDispose: false)
        {
            TryRecoverLatest = true,
            RemoveOutdatedCheckpoints = true,
            CheckpointDir = Path.Combine(path, "checkpoints"),
        });
        _checkpointHybrid = IssueHybridLogCheckpoints();
        _checkpointFull = IssueFullCheckpoints();
    }
    
    // These loops must await the checkpoint: blocking a thread-pool thread with
    // GetResult() from every live context starves the pool and deadlocks the
    // process once enough contexts run in one host (e.g. the test suite).
    private async Task IssueHybridLogCheckpoints()
    {
        while (!_disposed)
        {
            await Task.Delay(100);
            _ = await Store.TakeHybridLogCheckpointAsync(CheckpointType.FoldOver);
        }
    }
    private async Task IssueFullCheckpoints()
    {
        while (!_disposed)
        {
            await Task.Delay(1000);
            _ = await Store.TakeHybridLogCheckpointAsync(CheckpointType.FoldOver);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await _checkpointHybrid;
        await _checkpointFull;
        Store.Dispose();
    }
}