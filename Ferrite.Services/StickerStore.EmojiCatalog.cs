// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

public sealed partial class StickerStore
{
    public async ValueTask<TLBytes> GetEmojiIdCatalogueAsync(long requestedHash)
    {
        IReadOnlyCollection<TLStickerSetState> rows = await _repository
            .GetSetsAsync();
        try
        {
            long[] ids = FindDocumentIds(rows, StickerSetKind.Emoji,
                string.Empty, string.Empty).Distinct().Order().ToArray();
            long hash = HashIds(ids);
            if (requestedHash != 0 && requestedHash == hash)
                return EmojiListNotModified.Builder().Build().TLBytes!.Value;
            return EmojiList.Builder().Hash(hash).DocumentId(ToLongVector(ids))
                .Build().TLBytes!.Value;
        }
        finally
        {
            Dispose(rows);
        }
    }

    public async ValueTask<TLBytes> GetEmojiStatusCatalogueAsync(
        long requestedHash, bool collectibleOnly = false)
    {
        IReadOnlyCollection<TLStickerSetState> rows = await _repository
            .GetSetsAsync();
        try
        {
            long[] ids = collectibleOnly ? [] : FindDocumentIds(rows,
                    StickerSetKind.Emoji, string.Empty, string.Empty)
                .Distinct().Order().ToArray();
            long hash = HashIds(ids);
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
            Dispose(rows);
        }
    }
}
