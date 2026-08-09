// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Runtime.CompilerServices;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class NearbyLocationsRepository : INearbyLocationsRepository
{
    private readonly IKVStore _store;

    public NearbyLocationsRepository(IKVStore store)
    {
        _store = store;
        store.SetSchema(new TableDefinition("ferrite", "nearby_locations",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long })));
    }

    public bool PutLocation(TLNearbyLocation location) =>
        _store.Put(location.AsSpan().ToArray(), location.AsNearbyLocation().UserId);

    public async ValueTask<TLNearbyLocation?> GetLocationAsync(long userId)
    {
        byte[]? bytes = await _store.GetAsync(userId);
        return bytes is { Length: > 0 }
            ? new TLNearbyLocation(bytes, 0, bytes.Length)
            : null;
    }

    public bool DeleteLocation(long userId) => _store.Delete(userId);

    public async IAsyncEnumerable<TLNearbyLocation> IterateLocationsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (byte[] bytes in _store.IterateAsync()
                           .WithCancellation(cancellationToken))
        {
            if (bytes is { Length: > 0 })
            {
                yield return new TLNearbyLocation(bytes, 0, bytes.Length);
            }
        }
    }
}
