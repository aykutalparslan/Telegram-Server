// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

internal static class ChannelConverters
{
    internal static LayerObjectValue UpgradeSignatures(LayerConversionContext context,
        LayerObjectValue source)
    {
        var consumed = new HashSet<string>(StringComparer.Ordinal) { "enabled" };
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["signatures_enabled"] =
                BooleanConverters.IsTrue(source, "enabled") ? true : (object?)null
        };
        return context.ConvertCompatible(source, "channels.toggleSignatures",
            replacements, consumed);
    }

    internal static LayerObjectValue UpgradeCreator(LayerConversionContext context,
        LayerObjectValue source)
    {
        return ToPeerRequest(context, source, "messages.editChatCreator");
    }

    internal static LayerObjectValue ToPeerRequest(LayerConversionContext context,
        LayerObjectValue source, string target)
    {
        LayerObjectValue channel = source.Get<LayerObjectValue>("channel");
        var consumed = new HashSet<string>(StringComparer.Ordinal) { "channel" };
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["peer"] = context.ConvertCompatible(channel, PeerName(channel.Constructor.Name))
        };
        return context.ConvertCompatible(source, target, replacements, consumed);
    }

    private static string PeerName(string channel)
    {
        switch (channel)
        {
            case "inputChannelEmpty":
                return "inputPeerEmpty";
            case "inputChannel":
                return "inputPeerChannel";
            case "inputChannelFromMessage":
                return "inputPeerChannelFromMessage";
            default:
                throw new InvalidOperationException("The channel " + channel +
                                                    " has no peer form.");
        }
    }
}
