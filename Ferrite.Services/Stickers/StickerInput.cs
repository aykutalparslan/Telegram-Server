// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Stickers;

public readonly record struct StickerItemInput(long DocumentId,
    long AccessHash, string Emoji, byte[]? MaskCoords, string[] Keywords);

public static class StickerInput
{
    public static (long? Id, long? AccessHash, string? ShortName) ReadInputSet(
        InputStickerSetView input)
    {
        if (input.Is(out InputStickerSetID id))
        {
            return (id.Id, id.AccessHash, null);
        }
        if (input.Is(out InputStickerSetShortName named))
        {
            return (null, null, Encoding.UTF8.GetString(named.ShortName));
        }
        return (null, null, null);
    }

    public static (long? Id, long? AccessHash) ReadInputDocument(
        InputDocumentView input)
    {
        return input.Is(out InputDocument document)
            ? (document.Id, document.AccessHash)
            : (null, null);
    }

    public static StickerItemInput? ReadItem(InputStickerSetItemView input)
    {
        if (!input.Is(out InputStickerSetItem item)) return null;
        var document = ReadInputDocument(item.Get_DocumentView());
        if (!document.Id.HasValue || !document.AccessHash.HasValue) return null;
        return new StickerItemInput(document.Id.Value, document.AccessHash.Value,
            Encoding.UTF8.GetString(item.Emoji),
            item.Flags[0] ? item.MaskCoords.ToArray() : null,
            item.Flags[1]
                ? SplitKeywords(Encoding.UTF8.GetString(item.Keywords)) : []);
    }

    public static string[] SplitKeywords(string value) => value.Split(',',
        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
