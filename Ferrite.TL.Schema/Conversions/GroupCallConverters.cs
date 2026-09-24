// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

internal static class GroupCallConverters
{
    internal static LayerObjectValue DowngradeUpdate(
        LayerConversionContext context, LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (source.TryGetValue("peer", out object? value) &&
            value is LayerObjectValue peer)
        {
            string idField = peer.Constructor.Name switch
            {
                "peerChat" => "chat_id",
                "peerChannel" => "channel_id",
                _ => throw new InvalidOperationException(
                    "The group-call peer cannot be represented by layer 216.")
            };
            if (!peer.TryGetValue(idField, out object? id) || id is not long)
            {
                throw new InvalidOperationException(
                    "The group-call peer identifier is absent.");
            }
            replacements.Add("chat_id", id);
        }
        return context.ConvertCompatible(source, source.Constructor.Name,
            replacements);
    }
}
