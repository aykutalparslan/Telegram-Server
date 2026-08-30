// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.ChannelForums;

public sealed class DeleteTopicHistoryHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IForumTopicsRepository _forumTopicsRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICounterFactory _counterFactory;
    private readonly UpdateFanout _fanout;

    public DeleteTopicHistoryHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IChannelMessagesRepository channelMessagesRepository, IForumTopicsRepository forumTopicsRepository,
        ICounterFactory counterFactory, UpdateFanout fanout)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _channelMessagesRepository = channelMessagesRepository;
        _forumTopicsRepository = forumTopicsRepository;

        _unitOfWork = unitOfWork;
        _counterFactory = counterFactory;
        _fanout = fanout;
    }

    [TLFunction(Constructors.baseLayer_DeleteTopicHistory)]
    public async Task<Ferrite.TL.baseLayer.messages.TLAffectedHistory> Handle(
        long authKeyId, TLBytes q)
    {
        var request = (DeleteTopicHistory)q;
        long? channelId = ChannelForumAccess.ResolveInputChannelId(request.Get_ChannelView());
        int topicId = request.TopMsgId;
        if (topicId == 1)
            return ChannelForumErrors.AffectedHistory("TOPIC_ID_INVALID"u8);

        var (currentUserId, _, participantBytes, error) =
            await ChannelForumAccess.PrepareForumAccessAsync(_authorizationRepository, _chatRepository, _chatParticipantsRepository, authKeyId, channelId);
        if (error != null)
            return ChannelForumErrors.AffectedHistory(Encoding.UTF8.GetBytes(error));
        using var storedTopic = await _forumTopicsRepository
            .GetTopicAsync(channelId!.Value, topicId);
        if (storedTopic == null)
            return ChannelForumErrors.AffectedHistory("TOPIC_ID_INVALID"u8);
        StoredMessageForumTopic topic = ForumMessages.SnapshotMessageForumTopic(
            storedTopic.Value);

        var allMessages = await _channelMessagesRepository
            .GetMessagesAsync(channelId.Value);
        var deleteIds = new List<int>();
        bool onlyCreatorMessages = true;
        foreach (var saved in allMessages)
        {
            using var row = saved;
            var message = row.AsSavedMessage().Get_OriginalMessage();
            int messageId = MessageIds.GetId(message);
            if (ForumMessages.ResolveStoredForumTopicId(message.AsSpan(), messageId) != topicId)
                continue;
            deleteIds.Add(messageId);
            long senderId = ChannelForumTopics.ResolveMessageSenderId(message.AsSpan());
            if (senderId != currentUserId) onlyCreatorMessages = false;
        }
        bool canDelete = ChatRights.HasAdminRight(participantBytes,
            ChatAdminRightRequirement.DeleteMessages) ||
            (topic.CreatorId == currentUserId && onlyCreatorMessages && deleteIds.Count <= 11);
        if (!canDelete)
            return ChannelForumErrors.AffectedHistory("CHAT_ADMIN_REQUIRED"u8);

        foreach (int messageId in deleteIds)
        {
            await _channelMessagesRepository.DeleteMessageAsync(channelId.Value,
                messageId);
        }
        _forumTopicsRepository.DeleteTopic(channelId.Value, topicId);
        _forumTopicsRepository.DeleteReadStates(channelId.Value, topicId);

        var box = new ChannelMessageBox(_counterFactory, channelId.Value);
        int pts = deleteIds.Count == 0
            ? await box.Pts()
            : await box.IncrementPts(deleteIds.Count);
        if (deleteIds.Count > 0)
        {
            await _fanout.PushDeleteChannelMessagesAsync(channelId.Value, currentUserId,
                deleteIds, pts, deleteIds.Count);
        }
        await _unitOfWork.SaveAsync();
        return Ferrite.TL.baseLayer.messages.AffectedHistory.Builder()
            .Pts(pts).PtsCount(deleteIds.Count).Offset(0).Build();
    }
}
