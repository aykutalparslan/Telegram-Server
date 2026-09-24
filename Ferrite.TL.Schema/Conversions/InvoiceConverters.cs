// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

internal static class InvoiceConverters
{
    internal static LayerObjectValue Upgrade(LayerConversionContext context,
        LayerObjectValue source)
    {
        var consumed = new HashSet<string>(StringComparer.Ordinal)
        {
            "recurring_terms_url"
        };
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["terms_url"] =
                source.TryGetValue("recurring_terms_url", out object? url) ? url : null
        };
        return context.ConvertCompatible(source, "invoice", replacements, consumed);
    }
}
