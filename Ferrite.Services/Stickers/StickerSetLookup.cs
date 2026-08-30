// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Stickers;

public sealed class StickerSetLookup
{
    private readonly IStickerRepository _repository;

    public StickerSetLookup(IStickerRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<TLStickerSetState?> ResolveSetAsync(long? id,
        long? accessHash, string? shortName)
    {
        TLStickerSetState? row = id.HasValue
            ? await _repository.GetSetAsync(id.Value)
            : shortName is not null
                ? await _repository.GetSetByShortNameAsync(shortName)
                : null;
        if (row is null)
        {
            return null;
        }
        if (id.HasValue && (!accessHash.HasValue ||
            row.Value.AsStickerSetState().Get_SetView().AsStickerSet()
                .AccessHash != accessHash.Value))
        {
            row.Value.Dispose();
            return null;
        }
        return row;
    }

    public async ValueTask<long[]?> ResolveSetIdsAsync(
        IReadOnlyList<(long? Id, long? AccessHash, string? ShortName)> inputs)
    {
        var ids = new long[inputs.Count];
        for (int i = 0; i < inputs.Count; i++)
        {
            (long? id, long? accessHash, string? shortName) = inputs[i];
            using TLStickerSetState? row = await ResolveSetAsync(id, accessHash,
                shortName);
            if (row is null) return null;
            ids[i] = row.Value.AsStickerSetState().SetId;
        }
        return ids;
    }
}
