// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Messages;

public static class MessageReplyHeaders
{
    public static TLMessageReplyHeader FromInputReplyToMessage(
        InputReplyToMessage replyTo, bool forumTopic)
    {
        var builder = MessageReplyHeader.Builder()
            .ReplyToMsgId(replyTo.ReplyToMsgId);
        if (forumTopic) builder = builder.ForumTopic(true);
        if (replyTo.Flags[0]) builder = builder.ReplyToTopId(replyTo.TopMsgId);
        if (replyTo.Flags[2]) builder = builder.QuoteText(replyTo.QuoteText);
        if (replyTo.Flags[3]) builder = builder.QuoteEntities(replyTo.QuoteEntities);
        if (replyTo.Flags[4]) builder = builder.QuoteOffset(replyTo.QuoteOffset);
        if (replyTo.Flags[6]) builder = builder.TodoItemId(replyTo.TodoItemId);
        return builder.Build();
    }

    public static TLMessageReplyHeader ForForumTopic(int topicId) =>
        MessageReplyHeader.Builder()
            .ForumTopic(true)
            .ReplyToMsgId(topicId)
            .ReplyToTopId(topicId)
            .Build();
}
