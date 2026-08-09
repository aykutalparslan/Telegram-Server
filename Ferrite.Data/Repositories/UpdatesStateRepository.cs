// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class UpdatesStateRepository : IUpdatesStateRepository
{
    private readonly IKVStore _store;

    public UpdatesStateRepository(IKVStore store)
    {
        _store = store;
        store.SetSchema(new TableDefinition("ferrite", "updates_pts_state",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "pts", Type = DataType.Int })));
    }

    public bool PutPts(long userId, int pts)
    {
        using TLUpdatesPtsState state = UpdatesPtsState.Builder()
            .UserId(userId)
            .Pts(pts)
            .Build();
        return _store.Put(state.AsSpan().ToArray(), userId, pts);
    }

    public async ValueTask<int> GetPtsAsync(long userId)
    {
        int highWater = 0;
        await foreach (byte[] bytes in _store.IterateAsync(userId))
        {
            var state = new UpdatesPtsState(bytes);
            highWater = Math.Max(highWater, state.Pts);
        }
        return highWater;
    }
}
