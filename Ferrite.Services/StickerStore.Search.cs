// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services;

public sealed partial class StickerStore
{
    public async ValueTask<TLBytes> GetStickersAsync(string emoticon,
        long requestedHash)
    {
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            long[] ids = FindDocumentIds(rows, StickerSetKind.Regular,
                string.Empty, emoticon);
            long hash = HashIds(ids);
            if (requestedHash != 0 && requestedHash == hash)
            {
                var unchanged = StickersNotModified.Builder().Build();
                return unchanged.TLBytes!.Value;
            }
            var result = Stickers.Builder().Hash(hash)
                .StickersProperty(BuildDocuments(ids, rows, requiredKind:
                    StickerSetKind.Regular)).Build();
            return result.TLBytes!.Value;
        }
        finally
        {
            Dispose(rows);
        }
    }

    public async ValueTask<TLBytes> SearchCustomEmojiAsync(string emoticon,
        long requestedHash)
    {
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            long[] ids = FindDocumentIds(rows, StickerSetKind.Emoji,
                string.Empty, emoticon);
            long hash = HashIds(ids);
            if (requestedHash != 0 && requestedHash == hash)
            {
                var unchanged = EmojiListNotModified.Builder().Build();
                return unchanged.TLBytes!.Value;
            }
            var result = EmojiList.Builder().Hash(hash)
                .DocumentId(ToLongVector(ids)).Build();
            return result.TLBytes!.Value;
        }
        finally
        {
            Dispose(rows);
        }
    }

    public async ValueTask<TLBytes> GetCustomEmojiDocumentsAsync(long[] ids)
    {
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            return CopyVector(BuildDocuments(ids, rows, requiredKind:
                StickerSetKind.Emoji));
        }
        finally
        {
            Dispose(rows);
        }
    }

    public async ValueTask<TLBytes> SearchDocumentsAsync(bool emojis,
        string query, string emoticon, int offset, int limit,
        long requestedHash)
    {
        if (offset < 0 || limit is <= 0 or > CollectionLimit)
        {
            return Error("LIMIT_INVALID");
        }
        StickerSetKind kind = emojis ? StickerSetKind.Emoji
            : StickerSetKind.Regular;
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            long[] all = FindDocumentIds(rows, kind, query, emoticon);
            long hash = HashIds(all);
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
                .Stickers(BuildDocuments(page, rows, requiredKind: kind));
            if (nextOffset < all.Length)
            {
                resultBuilder = resultBuilder.NextOffset(nextOffset);
            }
            var result = resultBuilder.Build();
            return result.TLBytes!.Value;
        }
        finally
        {
            Dispose(rows);
        }
    }

    public async ValueTask<TLBytes> GetAttachedAsync(long? documentId,
        long? accessHash)
    {
        if (!documentId.HasValue)
        {
            return CopyVector(new Vector());
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
            return CopyVector(result);
        }
        finally
        {
            Dispose(rows);
        }
    }

    private static long[] FindDocumentIds(
        IReadOnlyCollection<TLStickerSetState> rows, StickerSetKind kind,
        string query, string emoticon)
    {
        var result = new List<long>();
        foreach (TLStickerSetState row in rows)
        {
            var view = row.AsStickerSetState();
            StickerSet set = view.Get_SetView().AsStickerSet();
            if (Kind(set) != kind) continue;

            HashSet<long>? emojiMatches = emoticon.Length == 0
                ? null : MatchingPackIds(view.Packs, emoticon);
            HashSet<long>? queryMatches = query.Length == 0
                ? null : MatchingKeywordIds(view.Keywords, query);
            if (queryMatches is not null &&
                (Encoding.UTF8.GetString(set.Title).Contains(query,
                     StringComparison.OrdinalIgnoreCase) ||
                 Encoding.UTF8.GetString(set.ShortName).Contains(query,
                     StringComparison.OrdinalIgnoreCase)))
            {
                queryMatches = null;
            }

            Vector documents = view.Documents;
            int count = documents.Count;
            for (int i = 0; i < count; i++)
            {
                var document = (DocumentView)documents.ReadTLObject();
                if (!document.Is(out Document value)) continue;
                if (emojiMatches is not null &&
                    !emojiMatches.Contains(value.Id)) continue;
                if (queryMatches is not null &&
                    !queryMatches.Contains(value.Id)) continue;
                if (!result.Contains(value.Id)) result.Add(value.Id);
            }
        }
        return result.ToArray();
    }

    private static HashSet<long> MatchingPackIds(Vector source,
        string emoticon)
    {
        var ids = new HashSet<long>();
        int count = source.Count;
        for (int i = 0; i < count; i++)
        {
            var pack = (StickerPack)source.ReadTLObject();
            if (Encoding.UTF8.GetString(pack.Emoticon).Contains(emoticon,
                    StringComparison.Ordinal))
            {
                ids.UnionWith(pack.Documents.ToArray());
            }
        }
        return ids;
    }

    private static HashSet<long> MatchingKeywordIds(Vector source,
        string query)
    {
        var ids = new HashSet<long>();
        int count = source.Count;
        for (int i = 0; i < count; i++)
        {
            var keyword = (StickerKeyword)source.ReadTLObject();
            VectorOfString values = keyword.Keyword;
            int valueCount = values.Count;
            for (int j = 0; j < valueCount; j++)
            {
                if (Encoding.UTF8.GetString(values.ReadTLBytes()).Contains(query,
                        StringComparison.OrdinalIgnoreCase))
                {
                    ids.Add(keyword.DocumentId);
                    break;
                }
            }
        }
        return ids;
    }

    private static TLBytes CopyVector(Vector vector)
    {
        byte[] bytes = vector.ToReadOnlySpan().ToArray();
        return new TLBytes(bytes, 0, bytes.Length);
    }
}
