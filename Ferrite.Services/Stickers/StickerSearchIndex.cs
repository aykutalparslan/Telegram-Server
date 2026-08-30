// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Stickers;

public sealed class StickerSearchIndex
{
    private readonly IStickerRepository _repository;
    private readonly StickerDocumentIndex _documents;

    public StickerSearchIndex(IStickerRepository repository,
        StickerDocumentIndex documents)
    {
        _repository = repository;
        _documents = documents;
    }

    public async ValueTask<TLBytes> GetStickersAsync(string emoticon,
        long requestedHash)
    {
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            long[] ids = StickerRows.FindDocumentIds(rows,
                StickerSetKind.Regular, string.Empty, emoticon);
            long hash = StickerRows.HashIds(ids);
            if (requestedHash != 0 && requestedHash == hash)
            {
                var unchanged = StickersNotModified.Builder().Build();
                return unchanged.TLBytes!.Value;
            }
            var result = Ferrite.TL.baseLayer.messages.Stickers.Builder().Hash(hash)
                .StickersProperty(_documents.BuildDocuments(ids, rows,
                    requiredKind: StickerSetKind.Regular)).Build();
            return result.TLBytes!.Value;
        }
        finally
        {
            StickerRows.Dispose(rows);
        }
    }

    public async ValueTask<TLBytes> SearchCustomEmojiAsync(string emoticon,
        long requestedHash)
    {
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            long[] ids = StickerRows.FindDocumentIds(rows, StickerSetKind.Emoji,
                string.Empty, emoticon);
            long hash = StickerRows.HashIds(ids);
            if (requestedHash != 0 && requestedHash == hash)
            {
                var unchanged = EmojiListNotModified.Builder().Build();
                return unchanged.TLBytes!.Value;
            }
            var result = EmojiList.Builder().Hash(hash)
                .DocumentId(StickerVectors.ToLongVector(ids)).Build();
            return result.TLBytes!.Value;
        }
        finally
        {
            StickerRows.Dispose(rows);
        }
    }

    public async ValueTask<TLBytes> GetCustomEmojiDocumentsAsync(long[] ids)
    {
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            return StickerVectors.CopyVector(_documents.BuildDocuments(ids, rows,
                requiredKind: StickerSetKind.Emoji));
        }
        finally
        {
            StickerRows.Dispose(rows);
        }
    }

    public async ValueTask<TLBytes> SearchDocumentsAsync(bool emojis,
        string query, string emoticon, int offset, int limit,
        long requestedHash)
    {
        if (offset < 0 || limit is <= 0 or > StickerRows.CollectionLimit)
        {
            return StickerResults.Error("LIMIT_INVALID");
        }
        StickerSetKind kind = emojis ? StickerSetKind.Emoji
            : StickerSetKind.Regular;
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            long[] all = StickerRows.FindDocumentIds(rows, kind, query, emoticon);
            long hash = StickerRows.HashIds(all);
            int nextOffset = Math.Min(offset + limit, all.Length);
            if (requestedHash != 0 && requestedHash == hash)
            {
                var builder = FoundStickersNotModified.Builder();
                if (nextOffset < all.Length) builder = builder.NextOffset(nextOffset);
                var unchanged = builder.Build();
                return unchanged.TLBytes!.Value;
            }
            long[] page = all.Skip(offset).Take(limit).ToArray();
            var resultBuilder = FoundStickers.Builder().Hash(hash)
                .Stickers(_documents.BuildDocuments(page, rows,
                    requiredKind: kind));
            if (nextOffset < all.Length)
            {
                resultBuilder = resultBuilder.NextOffset(nextOffset);
            }
            var result = resultBuilder.Build();
            return result.TLBytes!.Value;
        }
        finally
        {
            StickerRows.Dispose(rows);
        }
    }

    public async ValueTask<TLBytes> GetAttachedAsync(long? documentId,
        long? accessHash)
    {
        if (!documentId.HasValue)
        {
            return StickerVectors.CopyVector(new Vector());
        }
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            var result = new Vector();
            foreach (TLStickerSetState row in rows)
            {
                var view = row.AsStickerSetState();
                Vector documents = view.Documents;
                int count = documents.Count;
                bool contains = false;
                for (int i = 0; i < count; i++)
                {
                    var document = (DocumentView)documents.ReadTLObject();
                    if (document.Is(out Document value) &&
                        value.Id == documentId.Value &&
                        value.AccessHash == accessHash)
                    {
                        contains = true;
                        break;
                    }
                }
                if (!contains) continue;
                using var covered = StickerSetNoCovered.Builder()
                    .Set(view.Get_SetView().AsStickerSet().ToReadOnlySpan())
                    .Build();
                result.AppendTLObject(covered.ToReadOnlySpan());
            }
            return StickerVectors.CopyVector(result);
        }
        finally
        {
            StickerRows.Dispose(rows);
        }
    }
}
