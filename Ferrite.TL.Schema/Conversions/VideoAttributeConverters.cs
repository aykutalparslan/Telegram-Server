// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

internal static class VideoAttributeConverters
{
    internal static LayerObjectValue Upgrade(LayerConversionContext context,
        LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["duration"] = (double)source.Get<int>("duration")
        };
        return context.ConvertCompatible(source, "documentAttributeVideo", replacements);
    }

    internal static LayerObjectValue Downgrade(
        LayerConversionContext context, LayerObjectValue source)
    {
        if (!source.TryGetValue("duration", out object? value) ||
            value is not double duration)
        {
            throw new InvalidOperationException(
                "The video attribute has no duration.");
        }
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["duration"] = (int)Math.Ceiling(duration)
        };
        return context.ConvertCompatible(source, "documentAttributeVideo",
            replacements);
    }
}
