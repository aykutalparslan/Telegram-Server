// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using FASTER.core;

namespace Ferrite.Data.Primitives;

public class FasterContext<TKey, TValue> : IAsyncDisposable
{
    public FasterKV<TKey, TValue> Store { get; }

    public bool IsDurable { get; }

    private bool _disposed = false;
    private readonly Task? _checkpointHybrid;
    private readonly Task? _checkpointFull;
    
    public FasterContext()
    {
        IsDurable = false;
        Store = new FasterKV<TKey, TValue>(new FasterKVSettings<TKey, TValue>(null));
    }
    public FasterContext(string path)
    {
        IsDurable = true;
        Store = new FasterKV<TKey, TValue>(new FasterKVSettings<TKey, TValue>(path, deleteDirOnDispose: false)
        {
            TryRecoverLatest = true,
            RemoveOutdatedCheckpoints = true,
            CheckpointDir = Path.Combine(path, "checkpoints"),
        });
        _checkpointHybrid = IssueHybridLogCheckpoints();
        _checkpointFull = IssueFullCheckpoints();
    }
    
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
        if (_checkpointHybrid != null)
        {
            await _checkpointHybrid;
        }
        if (_checkpointFull != null)
        {
            await _checkpointFull;
        }
        Store.Dispose();
    }
}