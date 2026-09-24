// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

internal static class ProfileConverters
{
    internal static LayerObjectValue UpgradeEmojiStatus(LayerConversionContext context,
        LayerObjectValue source)
    {
        return context.ConvertCompatible(source, "emojiStatus");
    }

    internal static LayerObjectValue UpgradeColor(LayerConversionContext context,
        LayerObjectValue source)
    {
        var color = new Dictionary<string, object?>(StringComparer.Ordinal);
        context.CopyField(source, color, "color");
        context.CopyField(source, color, "background_emoji_id");
        var consumed = new HashSet<string>(StringComparer.Ordinal)
        {
            "background_emoji_id"
        };
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["color"] = color.Count == 0 ? null : context.Create("peerColor", color)
        };
        return context.ConvertCompatible(source, "account.updateColor", replacements,
            consumed);
    }
}
