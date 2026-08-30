// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Channels;

public sealed record StoredMessageForumTopic(long ChannelId, int TopicId,
    long CreatorId, int Date, byte[] Title, int IconColor, long IconEmojiId,
    int TopMessage, bool Closed, bool Hidden, int PinnedOrder);

internal static class ForumMessages
{
    public static async Task<string?> ValidateMessageAccessAsync(
        IChatRepository chatRepository,
        IChatParticipantsRepository chatParticipantsRepository,
        long channelId, long userId)
    {
        using (TLChat? channel = await chatRepository.GetChatAsync(channelId))
        {
            if (channel == null || channel.Value.Type != TLChat.ChatType.Channel ||
                !channel.Value.AsChannel().Forum)
            {
                return "CHANNEL_INVALID";
            }
        }

        using TLChatParticipantInfo? participant = await chatParticipantsRepository
            .GetParticipantAsync(channelId, userId);
        if (participant == null)
        {
            return "CHANNEL_PRIVATE";
        }

        int role = participant.Value.AsChatParticipantInfo().Role;
        return role == (int)ChatParticipantRole.Banned ||
               role == (int)ChatParticipantRole.Left
            ? "CHANNEL_PRIVATE"
            : null;
    }

    public static int ResolveRequestedForumTopicId(TLBytes sendMessage)
    {
        var request = (SendMessage)sendMessage;
        if (!request.Flags[0] ||
            !request.Get_ReplyToView().Is(out InputReplyToMessage reply))
            return 1;
        return reply.Flags[0] && reply.TopMsgId > 0 ? reply.TopMsgId : reply.ReplyToMsgId;
    }

    public static StoredMessageForumTopic SnapshotMessageForumTopic(TLForumTopicInfo stored)
    {
        var info = stored.AsForumTopicInfo();
        long creatorId = info.Get_FromIdView().Is(out PeerUser creator) ? creator.UserId : 0;
        return new StoredMessageForumTopic(info.ChannelId, info.TopicId, creatorId,
            info.Date, info.Title.ToArray(), info.IconColor, info.IconEmojiId,
            info.TopMessage, info.Closed, info.Hidden, info.PinnedOrder);
    }

    public static TLForumTopicInfo BuildStoredForumTopic(StoredMessageForumTopic topic)
    {
        using TLPeer creator = new PeerUser(topic.CreatorId);
        var builder = ForumTopicInfo.Builder().ChannelId(topic.ChannelId)
            .TopicId(topic.TopicId).Date(topic.Date).Title(topic.Title)
            .IconColor(topic.IconColor).TopMessage(topic.TopMessage)
            .FromId(creator.AsSpan()).PinnedOrder(topic.PinnedOrder);
        if (topic.IconEmojiId != 0) builder = builder.IconEmojiId(topic.IconEmojiId);
        if (topic.Closed) builder = builder.Closed(true);
        if (topic.Hidden) builder = builder.Hidden(true);
        return builder.Build();
    }

    public static TLForumTopicInfo BuildStoredForumTopic(long channelId, int topicId,
        long creatorId, int date, byte[] title, int iconColor, long iconEmojiId,
        int topMessage, bool closed, bool hidden, int pinnedOrder) =>
        BuildStoredForumTopic(new StoredMessageForumTopic(channelId, topicId, creatorId,
            date, title, iconColor, iconEmojiId, topMessage, closed, hidden, pinnedOrder));

    public static byte[] BuildWireForumTopic(StoredMessageForumTopic topic,
        long viewerId, TLForumTopicReadState? storedReadState)
    {
        int readInbox = 0;
        int readOutbox = 0;
        int unread = 0;
        int unreadMentions = 0;
        int unreadReactions = 0;
        if (storedReadState != null)
        {
            var state = storedReadState.Value.AsForumTopicReadState();
            readInbox = state.ReadInboxMaxId;
            readOutbox = state.ReadOutboxMaxId;
            unread = state.UnreadCount;
            unreadMentions = state.UnreadMentionsCount;
            unreadReactions = state.UnreadReactionsCount;
        }
        using TLPeer creator = new PeerUser(topic.CreatorId);
        using var notifySettings = PeerNotifySettings.Builder().Build();
        var builder = ForumTopic.Builder().Id(topic.TopicId).Date(topic.Date)
            .Title(topic.Title).IconColor(topic.IconColor).TopMessage(topic.TopMessage)
            .ReadInboxMaxId(readInbox).ReadOutboxMaxId(readOutbox)
            .UnreadCount(unread).UnreadMentionsCount(unreadMentions)
            .UnreadReactionsCount(unreadReactions).FromId(creator.AsSpan())
            .NotifySettings(notifySettings.ToReadOnlySpan());
        if (topic.CreatorId == viewerId) builder = builder.My(true);
        if (topic.IconEmojiId != 0) builder = builder.IconEmojiId(topic.IconEmojiId);
        if (topic.Closed) builder = builder.Closed(true);
        if (topic.Hidden) builder = builder.Hidden(true);
        if (topic.PinnedOrder > 0) builder = builder.Pinned(true);
        using TLForumTopic result = builder.Build();
        return result.AsSpan().ToArray();
    }

    public static int ResolveStoredForumTopicId(Span<byte> messageBytes, int messageId)
    {
        var message = (Ferrite.TL.baseLayer.Message)messageBytes;
        if (message.Constructor == Constructors.baseLayer_Message)
        {
            var regular = (Message)messageBytes;
            if (!regular.Flags[3] ||
                !regular.Get_ReplyToView().Is(out MessageReplyHeader header) ||
                !header.ForumTopic)
                return 1;
            return header.Flags[1] ? header.ReplyToTopId : header.ReplyToMsgId;
        }
        var service = (MessageService)messageBytes;
        if (service.Constructor != Constructors.baseLayer_MessageService) return 0;
        if (service.Get_ActionView().Is(out MessageActionTopicCreate _)) return messageId;
        if (!service.Flags[3] ||
            !service.Get_ReplyToView().Is(out MessageReplyHeader serviceHeader) ||
            !serviceHeader.ForumTopic)
            return 1;
        return serviceHeader.Flags[1]
            ? serviceHeader.ReplyToTopId
            : serviceHeader.ReplyToMsgId;
    }

    public static long ResolveStoredMessageSenderId(Span<byte> messageBytes)
    {
        var message = (Ferrite.TL.baseLayer.Message)messageBytes;
        if (message.Constructor == Constructors.baseLayer_Message)
        {
            var regular = (Message)messageBytes;
            return regular.Get_FromIdView().Is(out PeerUser user) ? user.UserId : 0;
        }
        var service = (MessageService)messageBytes;
        return service.Constructor == Constructors.baseLayer_MessageService &&
               service.Get_FromIdView().Is(out PeerUser serviceUser)
            ? serviceUser.UserId
            : 0;
    }
}
