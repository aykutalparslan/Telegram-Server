// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

internal static class StoryConverters
{
    internal static LayerObjectValue DowngradeRecentMaximum(
        LayerConversionContext context, LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (source.TryGetValue("stories_max_id", out object? value) &&
            value is LayerObjectValue recentStory &&
            recentStory.TryGetValue("max_id", out object? maximum))
        {
            replacements.Add("stories_max_id", maximum);
        }
        return context.ConvertCompatible(source, source.Constructor.Name,
            replacements);
    }
}
