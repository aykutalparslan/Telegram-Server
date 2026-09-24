// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Globalization;
using System.Text;

namespace Ferrite.TL.Schema;

internal static class StatisticsConverters
{
    internal static LayerObjectValue UpgradePublicForwards(LayerConversionContext context,
        LayerObjectValue source)
    {
        var consumed = new HashSet<string>(StringComparer.Ordinal)
        {
            "offset_rate",
            "offset_peer",
            "offset_id"
        };
        int rate = source.Get<int>("offset_rate");
        int messageId = source.Get<int>("offset_id");
        string offset = rate == 0 && messageId == 0
            ? string.Empty
            : rate.ToString(CultureInfo.InvariantCulture) + "_" +
              ChannelId(source).ToString(CultureInfo.InvariantCulture) + "_" +
              messageId.ToString(CultureInfo.InvariantCulture);
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["offset"] = Encoding.UTF8.GetBytes(offset)
        };
        return context.ConvertCompatible(source, "stats.getMessagePublicForwards",
            replacements, consumed);
    }

    private static long ChannelId(LayerObjectValue source)
    {
        if (source.TryGetValue("offset_peer", out object? value) &&
            value is LayerObjectValue peer &&
            peer.TryGetValue("channel_id", out object? channelId) &&
            channelId is long id)
        {
            return id;
        }
        return 0;
    }

    internal static LayerObjectValue DowngradeBroadcastStats(
        LayerConversionContext context, LayerObjectValue source)
    {
        if (!source.TryGetValue("recent_posts_interactions", out object? value) ||
            value is not IReadOnlyList<object?> elements)
        {
            throw new InvalidOperationException(
                "The broadcast statistics have no recent interactions.");
        }
        var interactions = new List<object?>(elements.Count);
        foreach (object? element in elements)
        {
            if (element is not LayerObjectValue counters)
            {
                throw new InvalidOperationException(
                    "The recent interactions are malformed.");
            }
            if (counters.Constructor.Name != "postInteractionCountersMessage")
            {
                continue;
            }
            interactions.Add(context.ConvertCompatible(counters,
                "messageInteractionCounters"));
        }
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["recent_message_interactions"] = interactions.AsReadOnly()
        };
        return context.ConvertCompatible(source, "stats.broadcastStats",
            replacements);
    }

    internal static LayerObjectValue DowngradePublicForwards(
        LayerConversionContext context, LayerObjectValue source)
    {
        if (!source.TryGetValue("forwards", out object? value) ||
            value is not IReadOnlyList<object?> forwards)
        {
            throw new InvalidOperationException("The public forwards are absent.");
        }
        var messages = new object?[forwards.Count];
        for (int index = 0; index < forwards.Count; index++)
        {
            if (forwards[index] is not LayerObjectValue forward ||
                forward.Constructor.Name != "publicForwardMessage" ||
                !forward.TryGetValue("message", out object? message) ||
                message is not LayerObjectValue body)
            {
                throw new InvalidOperationException("The public forward " +
                    (forwards[index] as LayerObjectValue)?.Constructor.Name +
                    " has no published form.");
            }
            messages[index] = context.Convert(body);
        }
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["messages"] = Array.AsReadOnly(messages)
        };
        if (source.TryGetValue("next_offset", out object? offset))
        {
            replacements.Add("next_rate", NextRate(offset));
        }
        return context.ConvertCompatible(source, "messages.messagesSlice",
            replacements);
    }

    private static int NextRate(object? offset)
    {
        string[] parts = offset is byte[] bytes
            ? Encoding.UTF8.GetString(bytes).Split('_')
            : Array.Empty<string>();
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture,
                out int rate))
        {
            throw new InvalidOperationException(
                "The public forwards offset has no published rate.");
        }
        return rate;
    }
}
