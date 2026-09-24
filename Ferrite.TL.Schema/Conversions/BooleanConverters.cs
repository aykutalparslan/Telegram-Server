// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

internal static class BooleanConverters
{
    internal static bool IsTrue(LayerObjectValue source, string field)
    {
        return source.TryGetValue(field, out object? value) &&
               value is LayerObjectValue flag &&
               string.Equals(flag.Constructor.Name, "boolTrue",
                   StringComparison.Ordinal);
    }

    internal static LayerObjectValue FromFlag(LayerConversionContext context,
        LayerObjectValue source, string field)
    {
        return Create(context, source.Fields.ContainsKey(field));
    }

    internal static LayerObjectValue Create(LayerConversionContext context, bool value)
    {
        return new LayerObjectValue(
            context.TargetSchema.GetConstructor(value ? "boolTrue" : "boolFalse"),
            Array.Empty<KeyValuePair<string, object?>>());
    }
}
