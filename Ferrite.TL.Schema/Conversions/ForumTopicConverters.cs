// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

internal static class ForumTopicConverters
{
    internal static LayerObjectValue UpgradeToggle(LayerConversionContext context,
        LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tabs"] = context.Create("boolFalse")
        };
        return context.ConvertCompatible(source, "channels.toggleForum", replacements);
    }

    internal static LayerObjectValue UpgradeRequest(LayerConversionContext context,
        LayerObjectValue source)
    {
        const string channels = "channels.";
        return ChannelConverters.ToPeerRequest(context, source,
            "messages." + source.Constructor.Name.Substring(channels.Length));
    }

    internal static LayerObjectValue DowngradePinnedTopic(
        LayerConversionContext context, LayerObjectValue source)
    {
        return DowngradePinnedUpdate(context, source,
            "updateChannelPinnedTopic");
    }

    internal static LayerObjectValue DowngradePinnedTopics(
        LayerConversionContext context, LayerObjectValue source)
    {
        return DowngradePinnedUpdate(context, source,
            "updateChannelPinnedTopics");
    }

    private static LayerObjectValue DowngradePinnedUpdate(
        LayerConversionContext context, LayerObjectValue source,
        string targetConstructor)
    {
        if (!source.TryGetValue("peer", out object? value) ||
            value is not LayerObjectValue peer ||
            peer.Constructor.Name != "peerChannel" ||
            !peer.TryGetValue("channel_id", out object? channelId) ||
            channelId is not long)
        {
            throw new InvalidOperationException(
                "The pinned-forum peer cannot be represented by layer 215.");
        }
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["channel_id"] = channelId
        };
        return context.ConvertCompatible(source, targetConstructor, replacements);
    }
}
