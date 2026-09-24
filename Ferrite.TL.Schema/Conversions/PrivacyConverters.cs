// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

internal static class PrivacyConverters
{
    internal static LayerObjectValue Upgrade(
        LayerConversionContext context, LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["archive_and_mute_new_noncontact_peers"] =
                BooleanConverters.IsTrue(source, "archive_and_mute_new_noncontact_peers")
                    ? true
                    : (object?)null
        };
        return context.ConvertCompatible(source, "globalPrivacySettings", replacements);
    }

    internal static LayerObjectValue Downgrade(
        LayerConversionContext context, LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["archive_and_mute_new_noncontact_peers"] = BooleanConverters.FromFlag(context,
                source, "archive_and_mute_new_noncontact_peers")
        };
        return context.ConvertCompatible(source, "globalPrivacySettings",
            replacements);
    }

    internal static LayerObjectValue DowngradePeerBlocked(
        LayerConversionContext context, LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["blocked"] = BooleanConverters.FromFlag(context, source, "blocked")
        };
        return context.ConvertCompatible(source, "updatePeerBlocked",
            replacements);
    }
}
