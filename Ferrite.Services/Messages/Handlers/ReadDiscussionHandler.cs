// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class ReadDiscussionHandler
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;

    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IForumTopicsRepository _forumTopicsRepository;

    private readonly IUnitOfWork _unitOfWork;

    public ReadDiscussionHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IForumTopicsRepository forumTopicsRepository)
    {
        _channelMessagesRepository = channelMessagesRepository;

        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _authorizationRepository = authorizationRepository;
        _forumTopicsRepository = forumTopicsRepository;

        _unitOfWork = unitOfWork;
    }

    [TLFunction(Constructors.baseLayer_ReadDiscussion)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null) return ErrorBool("AUTH_KEY_INVALID");
        long userId = auth.Value.AsAuthInfo().UserId;
        var request = (ReadDiscussion)q;
        if (!PeerResolver.TryResolveInputPeerDialogKey(request.Get_PeerView(), userId,
                out DialogPeerKey key) ||
            key.Type != TLPeer.PeerType.PeerChannel)
        {
            return ErrorBool("PEER_ID_INVALID");
        }
        long channelId = key.Id;
        int topicId = request.MsgId;
        int readMaxId = request.ReadMaxId;
        string? accessError = await ForumMessages.ValidateMessageAccessAsync(_chatRepository, _chatParticipantsRepository, channelId, userId);
        if (accessError != null) return ErrorBool(accessError);
        using TLForumTopicInfo? topic = await _forumTopicsRepository
            .GetTopicAsync(channelId, topicId);
        if (topic == null) return ErrorBool("TOPIC_ID_INVALID");

        int oldInbox = 0;
        int oldOutbox = 0;
        using (TLForumTopicReadState? oldState = await _forumTopicsRepository
                   .GetReadStateAsync(channelId, topicId, userId))
        {
            if (oldState != null)
            {
                var state = oldState.Value.AsForumTopicReadState();
                oldInbox = state.ReadInboxMaxId;
                oldOutbox = state.ReadOutboxMaxId;
            }
        }
        int nextInbox = Math.Max(oldInbox, readMaxId);
        int unread = 0;
        IReadOnlyCollection<TLSavedMessage> allMessages = await _channelMessagesRepository.GetMessagesAsync(channelId);
        foreach (TLSavedMessage row in allMessages)
        {
            using var saved = row;
            TLMessage message = saved.AsSavedMessage().Get_OriginalMessage();
            int id = MessageIds.GetId(message);
            if (id > nextInbox &&
                ForumMessages.ResolveStoredForumTopicId(message.AsSpan(), id) == topicId &&
                ForumMessages.ResolveStoredMessageSenderId(message.AsSpan()) != userId)
            {
                unread++;
            }
        }
        using TLForumTopicReadState updated = ForumTopicReadState.Builder()
            .ChannelId(channelId).TopicId(topicId).UserId(userId)
            .ReadInboxMaxId(nextInbox).ReadOutboxMaxId(oldOutbox)
            .UnreadCount(unread).UnreadMentionsCount(0).UnreadReactionsCount(0).Build();
        _forumTopicsRepository.PutReadState(updated);
        await _unitOfWork.SaveAsync();
        return new BoolTrue();
    }

    private static TLBool ErrorBool(string message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400,
            System.Text.Encoding.UTF8.GetBytes(message));
}
