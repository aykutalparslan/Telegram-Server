// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

internal static class ConfigConverters
{
    private const int SavedGifsLimit = 200;
    private const int StickersFavedLimit = 5;
    private const int PinnedDialogsCountMax = 5;
    private const int PinnedInFolderCountMax = 100;

    internal static LayerObjectValue Downgrade(LayerConversionContext context,
        LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["phonecalls_enabled"] = true,
            ["saved_gifs_limit"] = SavedGifsLimit,
            ["stickers_faved_limit"] = StickersFavedLimit,
            ["pinned_dialogs_count_max"] = PinnedDialogsCountMax,
            ["pinned_infolder_count_max"] = PinnedInFolderCountMax
        };
        return context.ConvertCompatible(source, "config", replacements);
    }
}
