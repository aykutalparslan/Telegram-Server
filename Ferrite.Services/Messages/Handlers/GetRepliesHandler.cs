// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetRepliesHandler
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;

    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IForumTopicsRepository _forumTopicsRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICounterFactory _counterFactory;
    private readonly UpdateFanout _fanout;

    public GetRepliesHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IForumTopicsRepository forumTopicsRepository, ICounterFactory counterFactory,
        UpdateFanout fanout)
    {
        _channelMessagesRepository = channelMessagesRepository;

        _chatParticipantsRepository = chatParticipantsRepository;

        _authorizationRepository = authorizationRepository;
        _chatRepository = chatRepository;
        _forumTopicsRepository = forumTopicsRepository;

        _unitOfWork = unitOfWork;
        _counterFactory = counterFactory;
        _fanout = fanout;
    }

    [TLFunction(Constructors.baseLayer_GetReplies)]
    public async Task<TLMessages> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (TLMessages)RpcErrorGenerator.GenerateError(400,
                "AUTH_KEY_INVALID"u8);
        }
        long userId = auth.Value.AsAuthInfo().UserId;

        var request = (GetReplies)q;
        if (!PeerResolver.TryResolveInputPeerDialogKey(request.Get_PeerView(), userId,
                out DialogPeerKey key) ||
            key.Type != TLPeer.PeerType.PeerChannel)
        {
            return (TLMessages)RpcErrorGenerator.GenerateError(400,
                "PEER_ID_INVALID"u8);
        }
        long channelId = key.Id;
        int topicId = request.MsgId;
        int offsetId = request.OffsetId;
        int offsetDate = request.OffsetDate;
        int addOffset = request.AddOffset;
        int limit = request.Limit;
        int maxId = request.MaxId;
        int minId = request.MinId;

        string? accessError = await ForumMessages.ValidateMessageAccessAsync(_chatRepository, _chatParticipantsRepository, channelId, userId);
        if (accessError != null)
        {
            return (TLMessages)RpcErrorGenerator.GenerateError(400,
                System.Text.Encoding.UTF8.GetBytes(accessError));
        }
        using TLForumTopicInfo? storedTopic = await _forumTopicsRepository
            .GetTopicAsync(channelId, topicId);
        if (storedTopic == null)
        {
            return (TLMessages)RpcErrorGenerator.GenerateError(400,
                "TOPIC_ID_INVALID"u8);
        }
        StoredMessageForumTopic topic = ForumMessages.SnapshotMessageForumTopic(
            storedTopic.Value);

        IReadOnlyCollection<TLSavedMessage> saved = await _channelMessagesRepository.GetMessagesAsync(channelId);
        var conversation = new List<(int Id, int Date, byte[] Bytes)>();
        foreach (TLSavedMessage row in saved)
        {
            using var messageRow = row;
            TLMessage message = messageRow.AsSavedMessage().Get_OriginalMessage();
            if (!MessageStore.TryReadStoredMessageInfo(message, out StoredMessageInfo info) ||
                ForumMessages.ResolveStoredForumTopicId(message.AsSpan(), info.Id) != topicId)
            {
                continue;
            }
            conversation.Add((info.Id, info.Date, info.Bytes));
        }
        int total = conversation.Count;
        int baseIndex = offsetId > 0
            ? conversation.Count(x => x.Id >= offsetId)
            : offsetDate > 0 ? conversation.Count(x => x.Date >= offsetDate) : 0;
        int start = Math.Clamp(baseIndex + addOffset, 0, conversation.Count);
        var selected = new List<byte[]>();
        for (int i = start; i < conversation.Count &&
             (limit <= 0 || selected.Count < limit); i++)
        {
            var item = conversation[i];
            if (maxId > 0 && item.Id > maxId) continue;
            if (minId > 0 && item.Id < minId) continue;
            selected.Add(item.Bytes);
        }

        using TLForumTopicReadState? readState = await _forumTopicsRepository
            .GetReadStateAsync(channelId, topicId, userId);
        byte[] topicBytes = ForumMessages.BuildWireForumTopic(topic, userId, readState);
        var relatedUsers = new HashSet<long> { topic.CreatorId };
        foreach (byte[] bytes in selected)
        {
            using var message = new TLMessage(bytes, 0, bytes.Length);
            var relatedChats = new HashSet<long>();
            MessageStore.AddMessageRelatedPeers(message, relatedUsers, relatedChats);
        }
        byte[] channelBytes;
        using (TLChat? channel = await _chatRepository.GetChatAsync(channelId))
        {
            channelBytes = channel!.Value.AsSpan().ToArray();
        }
        int pts = await new ChannelMessageBox(_counterFactory, channelId).Pts();

        var messages = new Vector();
        foreach (byte[] bytes in selected) messages.AppendTLObject(bytes);
        var topics = new Vector();
        topics.AppendTLObject(topicBytes);
        var chats = new Vector();
        chats.AppendTLObject(channelBytes);
        var users = new Vector();
        _fanout.AppendUsers(ref users, relatedUsers);
        return ChannelMessages.Builder().Pts(pts).Count(total)
            .Messages(messages).Topics(topics).Chats(chats).Users(users).Build();
    }
}
