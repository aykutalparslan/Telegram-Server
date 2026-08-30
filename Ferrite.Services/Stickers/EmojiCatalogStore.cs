// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Stickers;

public sealed class EmojiCatalogStore
{
    private readonly IStickerRepository _repository;

    public EmojiCatalogStore(IStickerRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<TLBytes> GetEmojiIdCatalogueAsync(long requestedHash)
    {
        IReadOnlyCollection<TLStickerSetState> rows = await _repository
            .GetSetsAsync();
        try
        {
            long[] ids = StickerRows.FindDocumentIds(rows, StickerSetKind.Emoji,
                string.Empty, string.Empty).Distinct().Order().ToArray();
            long hash = StickerRows.HashIds(ids);
            if (requestedHash != 0 && requestedHash == hash)
                return EmojiListNotModified.Builder().Build().TLBytes!.Value;
            return EmojiList.Builder().Hash(hash)
                .DocumentId(StickerVectors.ToLongVector(ids))
                .Build().TLBytes!.Value;
        }
        finally
        {
            StickerRows.Dispose(rows);
        }
    }

    public async ValueTask<TLBytes> GetEmojiStatusCatalogueAsync(
        long requestedHash, bool collectibleOnly = false)
    {
        IReadOnlyCollection<TLStickerSetState> rows = await _repository
            .GetSetsAsync();
        try
        {
            long[] ids = collectibleOnly ? [] : StickerRows.FindDocumentIds(rows,
                    StickerSetKind.Emoji, string.Empty, string.Empty)
                .Distinct().Order().ToArray();
            long hash = StickerRows.HashIds(ids);
            if (requestedHash != 0 && requestedHash == hash)
                return EmojiStatusesNotModified.Builder().Build().TLBytes!.Value;
            var statuses = new Vector();
            foreach (long id in ids)
            {
                using EmojiStatus status = EmojiStatus.Builder().DocumentId(id)
                    .Build();
                statuses.AppendTLObject(status.ToReadOnlySpan());
            }
            return EmojiStatuses.Builder().Hash(hash).Statuses(statuses).Build()
                .TLBytes!.Value;
        }
        finally
        {
            StickerRows.Dispose(rows);
        }
    }
}
