// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Reactions;

public sealed class GetUnreadReactionsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IMessageRepository _messageRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ReactionStore _reactions;
    private readonly ICounterFactory _counterFactory;
    private readonly UpdateFanout _fanout;
    private readonly ILogger _log;

    public GetUnreadReactionsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IMessageRepository messageRepository, ReactionStore reactions,
        ICounterFactory counterFactory, UpdateFanout fanout, ILogger log)
    {
        _authorizationRepository = authorizationRepository;
        _channelMessagesRepository = channelMessagesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _messageRepository = messageRepository;

        _unitOfWork = unitOfWork;
        _reactions = reactions;
        _counterFactory = counterFactory;
        _fanout = fanout;
        _log = log;
    }

    [TLFunction(Constructors.baseLayer_GetUnreadReactions)]
    public async Task<TLMessages> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return Error("AUTH_KEY_INVALID");
        }

        long userId = auth.Value.AsAuthInfo().UserId;
        var request = (GetUnreadReactions)q;
        long channelId = PeerResolver.ResolveInputPeerChannelId(request.Get_PeerView());
        (TLPeer.PeerType peerType, long peerId) = PeerResolver.ResolveHistoryPeer(request.Get_PeerView(), userId);
        int offsetId = request.OffsetId;
        int limit = request.Limit;
        int maxId = request.MaxId;
        int minId = request.MinId;
        if (channelId <= 0 && peerId <= 0)
        {
            return Error("PEER_ID_INVALID");
        }

        List<int> messageIds;
        if (channelId > 0)
        {
            var participant = await _chatParticipantsRepository
                .GetParticipantAsync(channelId, userId);
            bool activeMember = participant != null && IsActive(participant.Value);
            participant?.Dispose();
            if (!activeMember)
            {
                return Error("CHANNEL_PRIVATE");
            }
            messageIds = await _reactions.GatherUnreadReactionMessageIds(
                MessageReactionBox.Channel, channelId, userId,
                requireChannelAuthor: true, peerType, peerId);
        }
        else
        {
            messageIds = await _reactions.GatherUnreadReactionMessageIds(
                MessageReactionBox.Common, userId, userId,
                requireChannelAuthor: false, peerType, peerId);
        }

        messageIds.Sort((a, b) => b.CompareTo(a));
        IEnumerable<int> filtered = messageIds;
        if (maxId > 0)
        {
            filtered = filtered.Where(id => id < maxId);
        }
        if (minId > 0)
        {
            filtered = filtered.Where(id => id > minId);
        }
        if (offsetId > 0)
        {
            filtered = filtered.Where(id => id < offsetId);
        }
        int pageSize = limit is > 0 and <= 100 ? limit : 50;
        var pageIds = filtered.Take(pageSize).ToList();

        var messageBytes = new List<byte[]>();
        var relatedUserIds = new HashSet<long> { userId };
        var relatedChatIds = new HashSet<long>();
        foreach (int id in pageIds)
        {
            var saved = channelId > 0
                ? await _channelMessagesRepository.GetMessageAsync(channelId, id)
                : await _messageRepository.GetMessageAsync(userId, id);
            if (saved == null)
            {
                continue;
            }
            using var savedMessage = saved.Value;
            var message = savedMessage.AsSavedMessage().Get_OriginalMessage();
            messageBytes.Add(message.AsSpan().ToArray());
            MessageStore.AddMessageRelatedPeers(message, relatedUserIds, relatedChatIds);
        }
        if (channelId > 0)
        {
            relatedChatIds.Add(channelId);
        }

        var relatedChatBytes = await _fanout.GetChatBytesForViewerAsync(userId,
            relatedChatIds);
        int channelPts = 0;
        if (channelId > 0)
        {
            var channelBox = new ChannelMessageBox(_counterFactory, channelId);
            channelPts = await channelBox.Pts();
        }

        var messageVector = new Vector();
        foreach (byte[] message in messageBytes)
        {
            messageVector.AppendTLObject(message);
        }
        var userVector = new Vector();
        _fanout.AppendUsers(userId, ref userVector, relatedUserIds);
        var chatVector = new Vector();
        foreach (byte[] chat in relatedChatBytes)
        {
            chatVector.AppendTLObject(chat);
        }

        _log.Debug($"💟 GetUnreadReactions user:{userId} " +
                   $"peer:{(channelId > 0 ? channelId : peerId)} count:{messageBytes.Count}");
        if (channelId > 0)
        {
            return ChannelMessages.Builder()
                .Pts(channelPts)
                .Count(messageBytes.Count)
                .Messages(messageVector)
                .Topics(new Vector())
                .Chats(chatVector)
                .Users(userVector)
                .Build();
        }

        return Ferrite.TL.baseLayer.messages.Messages.Builder()
            .MessagesProperty(messageVector)
            .Chats(chatVector)
            .Users(userVector)
            .Build();
    }

    private static bool IsActive(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }

    private static TLMessages Error(string message) =>
        (TLMessages)RpcErrorGenerator.GenerateError(400,
            System.Text.Encoding.UTF8.GetBytes(message));
}
