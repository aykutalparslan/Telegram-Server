// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

internal static class DialogFilterConverters
{
    internal static LayerObjectValue Upgrade(LayerConversionContext context,
        LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["title"] = TextConverters.WithEntities(context, source, "title")
        };
        return context.ConvertCompatible(source, "dialogFilter", replacements);
    }

    internal static LayerObjectValue Downgrade(
        LayerConversionContext context, LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["title"] = TextConverters.PlainText(source, "title")
        };
        return context.ConvertCompatible(source, "dialogFilter", replacements);
    }
}
