// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class TopPeersRepository : ITopPeersRepository
{
    private readonly IKVStore _store;

    public TopPeersRepository(IKVStore store)
    {
        _store = store;
        store.SetSchema(new TableDefinition("ferrite", "top_peers_state",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long })));
    }

    public bool PutState(TLTopPeersState state) =>
        _store.Put(state.AsSpan().ToArray(), state.AsTopPeersState().UserId);

    public async ValueTask<TLTopPeersState?> GetStateAsync(long userId)
    {
        byte[]? bytes = await _store.GetAsync(userId);
        return bytes is { Length: > 0 }
            ? new TLTopPeersState(bytes, 0, bytes.Length)
            : null;
    }
}
