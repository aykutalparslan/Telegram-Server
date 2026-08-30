// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Stickers;

internal static class StickerRows
{
    public const int CollectionLimit = 200;

    public static void Dispose(IEnumerable<TLStickerSetState> rows)
    {
        foreach (TLStickerSetState row in rows)
        {
            row.Dispose();
        }
    }

    public static StickerSetKind Kind(StickerSet set) => set.Emojis
        ? StickerSetKind.Emoji
        : set.Masks ? StickerSetKind.Mask : StickerSetKind.Regular;

    public static bool MatchesKind(StickerSet set, StickerSetKind kind) =>
        kind switch
        {
            StickerSetKind.Mask => set.Masks,
            StickerSetKind.Emoji => set.Emojis,
            _ => !set.Masks && !set.Emojis,
        };

    public static bool MatchesQuery(StickerSet set, string query)
    {
        if (query.Length == 0)
        {
            return true;
        }
        return Encoding.UTF8.GetString(set.Title).Contains(query,
                   StringComparison.OrdinalIgnoreCase) ||
               Encoding.UTF8.GetString(set.ShortName).Contains(query,
                   StringComparison.OrdinalIgnoreCase);
    }

    public static long Hash(IEnumerable<TLStickerSetState> rows)
    {
        long hash = 1;
        foreach (TLStickerSetState row in rows)
        {
            var view = row.AsStickerSetState();
            hash = unchecked(hash * 20261 + view.SetId * 31 + view.Revision);
        }
        return hash;
    }

    public static long HashIds(IEnumerable<long> ids)
    {
        long hash = 1;
        foreach (long id in ids) hash = unchecked(hash * 20261 + id);
        return hash;
    }

    public static bool IsAnimated(Document document)
    {
        Vector attributes = document.Attributes;
        int count = attributes.Count;
        for (int i = 0; i < count; i++)
        {
            var attribute = (DocumentAttributeView)attributes.ReadTLObject();
            if (attribute.Is(out DocumentAttributeAnimated _)) return true;
        }
        return false;
    }

    public static Vector BuildPacks(IEnumerable<long> ids,
        IReadOnlyCollection<TLStickerSetState> rows)
    {
        HashSet<long> wanted = ids.ToHashSet();
        var result = new Vector();
        foreach (TLStickerSetState row in rows)
        {
            Vector packs = row.AsStickerSetState().Packs;
            int count = packs.Count;
            for (int i = 0; i < count; i++)
            {
                Span<byte> bytes = packs.ReadTLObject();
                var pack = (StickerPack)bytes;
                if (pack.Documents.ToArray().Any(wanted.Contains))
                {
                    result.AppendTLObject(bytes);
                }
            }
        }
        return result;
    }

    public static long[] FindDocumentIds(
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
}
