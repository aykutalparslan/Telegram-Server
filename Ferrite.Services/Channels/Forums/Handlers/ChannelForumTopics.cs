// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.ChannelForums;

internal static class ChannelForumTopics
{
    private sealed record ForumTopicPageItem(StoredMessageForumTopic Topic,
        int TopDate, byte[]? TopMessageBytes);

    internal static async Task<Ferrite.TL.baseLayer.messages.TLForumTopics> GetAsync(
        IAuthorizationRepository authorizationRepository,
        IChatRepository chatRepository,
        IChatParticipantsRepository chatParticipantsRepository,
        IChannelMessagesRepository channelMessagesRepository,
        IForumTopicsRepository forumTopicsRepository,
        UserSerializer userSerializer, ICounterFactory counterFactory, long authKeyId,
        long? channelId, string query, int offsetDate, int offsetId,
        int offsetTopic, int limit, IReadOnlyCollection<int>? requestedIds)
    {
        var (currentUserId, channelBytes, _, error) =
            await ChannelForumAccess.PrepareForumAccessAsync(authorizationRepository,
                chatRepository, chatParticipantsRepository, authKeyId, channelId);
        if (error != null)
            return ChannelForumErrors.ForumTopics(Encoding.UTF8.GetBytes(error));

        List<StoredMessageForumTopic> topics = await SnapshotAsync(
            forumTopicsRepository, channelId!.Value);
        if (requestedIds != null)
        {
            var byId = topics.ToDictionary(x => x.TopicId);
            topics = requestedIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
        }
        else
        {
            topics = topics.Where(x => query.Length == 0 ||
                    Encoding.UTF8.GetString(x.Title).Contains(query,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var pageItems = new List<ForumTopicPageItem>(topics.Count);
        foreach (StoredMessageForumTopic topic in topics)
        {
            byte[]? topMessageBytes = null;
            int topDate = topic.Date;
            using var topMessage = await channelMessagesRepository
                .GetMessageAsync(topic.ChannelId, topic.TopMessage);
            if (topMessage != null)
            {
                var original = topMessage.Value.AsSavedMessage().Get_OriginalMessage();
                topMessageBytes = original.AsSpan().ToArray();
                topDate = ResolveMessageDate(original.AsSpan(), topic.Date);
            }
            pageItems.Add(new ForumTopicPageItem(topic, topDate, topMessageBytes));
        }

        int total = pageItems.Count;
        if (requestedIds == null)
        {
            pageItems = pageItems
                .OrderByDescending(x => x.Topic.PinnedOrder > 0)
                .ThenByDescending(x => x.Topic.PinnedOrder)
                .ThenByDescending(x => x.TopDate)
                .ThenByDescending(x => x.Topic.TopMessage)
                .ThenByDescending(x => x.Topic.TopicId)
                .ToList();
            if (offsetTopic != 0)
            {
                int index = pageItems.FindIndex(x => x.Topic.TopicId == offsetTopic);
                if (index >= 0) pageItems = pageItems.Skip(index + 1).ToList();
            }
            else if (offsetDate != 0 || offsetId != 0)
            {
                pageItems = pageItems.Where(x =>
                        (offsetDate > 0 && x.TopDate < offsetDate) ||
                        (offsetDate > 0 && x.TopDate == offsetDate && offsetId > 0 &&
                         x.Topic.TopMessage < offsetId) ||
                        (offsetDate <= 0 && offsetId > 0 && x.Topic.TopMessage < offsetId))
                    .ToList();
            }
            pageItems = pageItems.Take(Math.Clamp(limit, 0, 100)).ToList();
        }

        var builtTopics = new List<byte[]>(pageItems.Count);
        var topMessages = new List<byte[]>();
        var relatedUserIds = new HashSet<long>();
        foreach (ForumTopicPageItem item in pageItems)
        {
            StoredMessageForumTopic topic = item.Topic;
            using var readState = await forumTopicsRepository.GetReadStateAsync(
                topic.ChannelId, topic.TopicId, currentUserId);
            builtTopics.Add(ForumMessages.BuildWireForumTopic(topic, currentUserId,
                readState));
            relatedUserIds.Add(topic.CreatorId);
            if (item.TopMessageBytes != null)
            {
                topMessages.Add(item.TopMessageBytes);
                long senderId = ResolveMessageSenderId(item.TopMessageBytes.AsSpan());
                if (senderId > 0) relatedUserIds.Add(senderId);
            }
        }
        int pts = await new ChannelMessageBox(counterFactory, channelId.Value).Pts();

        var topicVector = new Vector();
        foreach (byte[] topic in builtTopics) topicVector.AppendTLObject(topic);
        var messageVector = new Vector();
        foreach (byte[] message in topMessages) messageVector.AppendTLObject(message);
        var chatVector = new Vector();
        chatVector.AppendTLObject(channelBytes);
        var userVector = new Vector();
        AppendUsers(currentUserId, userSerializer, ref userVector, relatedUserIds);
        return Ferrite.TL.baseLayer.messages.ForumTopics.Builder()
            .Count(total).Topics(topicVector).Messages(messageVector)
            .Chats(chatVector).Users(userVector).Pts(pts).Build();
    }

    internal static async Task<List<StoredMessageForumTopic>> SnapshotAsync(
        IForumTopicsRepository forumTopicsRepository, long channelId)
    {
        var stored = await forumTopicsRepository.GetTopicsAsync(channelId);
        var result = new List<StoredMessageForumTopic>(stored.Count);
        foreach (var topic in stored)
        {
            using var row = topic;
            result.Add(ForumMessages.SnapshotMessageForumTopic(row));
        }
        return result;
    }

    internal static long ResolveMessageSenderId(Span<byte> messageSpan)
    {
        var message = (Ferrite.TL.baseLayer.Message)messageSpan;
        if (message.Constructor == Constructors.baseLayer_Message)
            return message.Get_FromIdView().Is(out PeerUser user) ? user.UserId : 0;

        var service = (MessageService)messageSpan;
        return service.Constructor == Constructors.baseLayer_MessageService &&
               service.Get_FromIdView().Is(out PeerUser serviceUser)
            ? serviceUser.UserId
            : 0;
    }

    private static int ResolveMessageDate(Span<byte> messageBytes, int fallback)
    {
        var message = (Ferrite.TL.baseLayer.Message)messageBytes;
        if (message.Constructor == Constructors.baseLayer_Message)
            return ((Message)messageBytes).Date;
        var service = (MessageService)messageBytes;
        return service.Constructor == Constructors.baseLayer_MessageService
            ? service.Date
            : fallback;
    }

    private static void AppendUsers(long viewerUserId, UserSerializer userSerializer, ref Vector userVector,
        IEnumerable<long> userIds)
    {
        var seen = new HashSet<long>();
        foreach (long userId in userIds)
        {
            if (!seen.Add(userId)) continue;
            userSerializer.Append(viewerUserId, ref userVector, userId);
        }
    }
}
