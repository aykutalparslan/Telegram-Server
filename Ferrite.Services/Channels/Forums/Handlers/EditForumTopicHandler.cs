// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.ChannelForums;

public sealed class EditForumTopicHandler
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IForumTopicsRepository _forumTopicsRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICounterFactory _counterFactory;
    private readonly ILogger _log;
    private readonly UpdateFanout _fanout;

    public EditForumTopicHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IForumTopicsRepository forumTopicsRepository,
        ICounterFactory counterFactory, ILogger log, UpdateFanout fanout)
    {
        _channelMessagesRepository = channelMessagesRepository;

        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _forumTopicsRepository = forumTopicsRepository;

        _unitOfWork = unitOfWork;
        _counterFactory = counterFactory;
        _log = log;
        _fanout = fanout;
    }

    [TLFunction(Constructors.baseLayer_EditForumTopic)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (EditForumTopic)q;
        long? channelId = ChannelForumAccess.ResolveInputChannelId(request.Get_ChannelView());
        int topicId = request.TopicId;
        bool hasTitle = request.Flags[0];
        byte[] title = request.Title.ToArray();
        bool hasIconEmoji = request.Flags[1];
        long iconEmojiId = request.IconEmojiId;
        bool hasClosed = request.Flags[2];
        bool closed = request.Closed;
        bool hasHidden = request.Flags[3];
        bool hidden = request.Hidden;
        if (hasTitle && title.Length == 0)
            return ChannelForumErrors.Updates("TOPIC_TITLE_EMPTY"u8);

        var (currentUserId, channelBytes, participantBytes, error) =
            await ChannelForumAccess.PrepareForumAccessAsync(_authorizationRepository, _chatRepository, _chatParticipantsRepository, authKeyId, channelId);
        if (error != null)
            return ChannelForumErrors.Updates(Encoding.UTF8.GetBytes(error));
        using var storedTopic = await _forumTopicsRepository
            .GetTopicAsync(channelId!.Value, topicId);
        if (storedTopic == null)
            return ChannelForumErrors.Updates("TOPIC_ID_INVALID"u8);
        StoredMessageForumTopic topic = ForumMessages.SnapshotMessageForumTopic(
            storedTopic.Value);
        bool canManage = ChatRights.HasAdminRight(participantBytes,
            ChatAdminRightRequirement.ManageTopics);
        if (!canManage && topic.CreatorId != currentUserId)
            return ChannelForumErrors.Updates("CHAT_ADMIN_REQUIRED"u8);
        if (hasHidden && topicId != 1)
            return ChannelForumErrors.Updates("TOPIC_ID_INVALID"u8);
        if (hasHidden && !canManage)
            return ChannelForumErrors.Updates("CHAT_ADMIN_REQUIRED"u8);
        if (hasIconEmoji && topicId == 1)
            return ChannelForumErrors.Updates("TOPIC_ID_INVALID"u8);
        if (!hasTitle && !hasIconEmoji && !hasClosed && !hasHidden)
            return ChannelForumErrors.Updates("TOPIC_NOT_MODIFIED"u8);

        bool nextHidden = hasHidden ? hidden : topic.Hidden;
        bool nextClosed = hasHidden ? hidden : hasClosed ? closed : topic.Closed;
        byte[] nextTitle = hasTitle ? title : topic.Title;
        long nextIconEmoji = hasIconEmoji ? iconEmojiId : topic.IconEmojiId;

        byte[] actionBytes;
        {
            var actionBuilder = MessageActionTopicEdit.Builder();
            if (hasTitle) actionBuilder = actionBuilder.Title(title);
            if (hasIconEmoji) actionBuilder = actionBuilder.IconEmojiId(iconEmojiId);
            if (hasClosed) actionBuilder = actionBuilder.Closed(closed);
            if (hasHidden) actionBuilder = actionBuilder.Hidden(hidden);
            using TLMessageAction action = actionBuilder.Build();
            actionBytes = action.AsSpan().ToArray();
        }
        byte[] replyHeaderBytes;
        using (TLMessageReplyHeader replyHeader = MessageReplyHeader.Builder()
                   .ForumTopic(true).ReplyToMsgId(topicId).Build())
        {
            replyHeaderBytes = replyHeader.AsSpan().ToArray();
        }

        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        var (serviceMessageBytes, pts) =
            await ChannelForumUpdates.WriteChannelServiceMessageAsync(_channelMessagesRepository, _counterFactory, channelId.Value, currentUserId, actionBytes, date,
                replyHeaderBytes);
        int messageId = ((MessageService)serviceMessageBytes.AsSpan()).Id;
        using (TLForumTopicInfo updated = ForumMessages.BuildStoredForumTopic(topic.ChannelId,
                   topic.TopicId, topic.CreatorId, topic.Date, nextTitle, topic.IconColor,
                   nextIconEmoji, messageId, nextClosed, nextHidden, topic.PinnedOrder))
        {
            _forumTopicsRepository.PutTopic(updated);
        }

        byte[] updateBytes;
        using (TLUpdate update = UpdateNewChannelMessage.Builder()
                   .Message(serviceMessageBytes).Pts(pts).PtsCount(1).Build())
        {
            updateBytes = update.AsSpan().ToArray();
        }
        await _fanout.PushChannelServiceMessageAsync(channelId.Value, currentUserId,
            serviceMessageBytes, pts);
        _log.Debug($"📣 EditForumTopic user:{currentUserId} channel:{channelId.Value} topic:{topicId}");
        return await ChannelForumUpdates.BuildForumResultAsync(_unitOfWork, _fanout,
            authKeyId, currentUserId, channelBytes, new[] { updateBytes });
    }
}
