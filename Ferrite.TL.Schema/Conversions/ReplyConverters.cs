// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

internal static class ReplyConverters
{
    internal static LayerObjectValue UpgradeRequest(LayerConversionContext context,
        LayerObjectValue source)
    {
        var consumed = new HashSet<string>(StringComparer.Ordinal)
        {
            "reply_to_msg_id",
            "top_msg_id"
        };
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["reply_to"] = ReplyTo(context, source)
        };
        return context.ConvertCompatible(source, source.Constructor.Name,
            replacements, consumed);
    }

    private static object? ReplyTo(LayerConversionContext context,
        LayerObjectValue source)
    {
        bool hasReply = source.TryGetValue("reply_to_msg_id",
            out object? replyToMsgId) && replyToMsgId is int;
        bool hasTop = source.TryGetValue("top_msg_id", out object? topMsgId) &&
                      topMsgId is int;
        if (!hasReply && !hasTop)
        {
            return null;
        }
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["reply_to_msg_id"] = hasReply ? replyToMsgId : topMsgId
        };
        if (hasTop)
        {
            fields.Add("top_msg_id", topMsgId);
        }
        return context.Create("inputReplyToMessage", fields);
    }

    internal static LayerObjectValue DowngradeDraft(
        LayerConversionContext context, LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (source.TryGetValue("reply_to", out object? value))
        {
            if (value is not LayerObjectValue replyTo ||
                replyTo.Constructor.Name != "inputReplyToMessage" ||
                !replyTo.TryGetValue("reply_to_msg_id", out object? messageId) ||
                messageId is not int)
            {
                throw new InvalidOperationException(
                    "The draft reply target has no published form.");
            }
            replacements.Add("reply_to_msg_id", messageId);
        }
        return context.ConvertCompatible(source, "draftMessage", replacements);
    }
}
