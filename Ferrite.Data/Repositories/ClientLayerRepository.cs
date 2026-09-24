// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class ClientLayerRepository : IClientLayerRepository
{
    private readonly IKVStore _store;

    public ClientLayerRepository(IKVStore store)
    {
        _store = store;
        _store.SetSchema(new TableDefinition("ferrite", "client_layers",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long })));
    }

    public bool PutClientLayer(TLClientLayer layer)
    {
        var value = layer.AsClientLayer();
        return _store.Put(layer.AsSpan().ToArray(), value.AuthKeyId);
    }

    public TLClientLayer? GetClientLayer(long authKeyId)
    {
        byte[]? bytes = _store.Get(authKeyId);
        return bytes == null ? null : new TLClientLayer(bytes, 0, bytes.Length);
    }

    public async ValueTask<TLClientLayer?> GetClientLayerAsync(long authKeyId)
    {
        byte[]? bytes = await _store.GetAsync(authKeyId);
        return bytes == null ? null : new TLClientLayer(bytes, 0, bytes.Length);
    }

    public IReadOnlyList<TLClientLayer> GetClientLayers() => _store.Iterate()
        .Select(bytes => new TLClientLayer(bytes, 0, bytes.Length))
        .ToArray();

    public bool DeleteClientLayer(long authKeyId) => _store.Delete(authKeyId);
}
