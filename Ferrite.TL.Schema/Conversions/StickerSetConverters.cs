// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

internal static class StickerSetConverters
{
    internal static LayerObjectValue UpgradeCreate(LayerConversionContext context,
        LayerObjectValue source)
    {
        var consumed = new HashSet<string>(StringComparer.Ordinal)
        {
            "animated",
            "videos"
        };
        return context.ConvertCompatible(source, "stickers.createStickerSet", null,
            consumed);
    }

    internal static LayerObjectValue DowngradeNoCoveredSet(
        LayerConversionContext context, LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["covers"] = Array.Empty<object?>()
        };
        return context.ConvertCompatible(source, "stickerSetMultiCovered",
            replacements);
    }
}
