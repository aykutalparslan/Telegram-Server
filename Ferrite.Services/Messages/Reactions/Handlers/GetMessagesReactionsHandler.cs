// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.Reactions;

public sealed class GetMessagesReactionsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IMessageReactionsRepository _messageReactionsRepository;
    private readonly IMessageRepository _messageRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateFanout _fanout;

    public GetMessagesReactionsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageReactionsRepository messageReactionsRepository, IMessageRepository messageRepository, UpdateFanout fanout)
    {
        _authorizationRepository = authorizationRepository;
        _channelMessagesRepository = channelMessagesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _messageReactionsRepository = messageReactionsRepository;
        _messageRepository = messageRepository;

        _unitOfWork = unitOfWork;
        _fanout = fanout;
    }

    [TLFunction(Constructors.baseLayer_GetMessagesReactions)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return Error("AUTH_KEY_INVALID");
        }

        long userId = auth.Value.AsAuthInfo().UserId;
        long channelId;
        TLPeer.PeerType peerType;
        long peerId;
        var ids = new List<int>();
        byte[] callerPeerBytes;
        {
            var request = (GetMessagesReactions)q;
            channelId = PeerResolver.ResolveInputPeerChannelId(request.Get_PeerView());
            (peerType, peerId) = PeerResolver.ResolveHistoryPeer(request.Get_PeerView(), userId);
            var idVector = request.Id;
            int count = idVector.Count;
            for (int i = 0; i < count; i++)
            {
                ids.Add(idVector[i]);
            }
            using TLPeer callerPeer = channelId > 0
                ? new PeerChannel(channelId)
                : PeerResolver.BuildPeer(peerType, peerId);
            callerPeerBytes = callerPeer.AsSpan().ToArray();
        }

        bool broadcast = false;
        if (channelId > 0)
        {
            using var channel = await _chatRepository.GetChatAsync(channelId);
            if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
            {
                return Error("CHANNEL_INVALID");
            }
            broadcast = channel.Value.AsChannel().Broadcast;
            var participant = await _chatParticipantsRepository
                .GetParticipantAsync(channelId, userId);
            bool activeMember = participant != null && IsActive(participant.Value);
            participant?.Dispose();
            if (!activeMember)
            {
                return Error("CHANNEL_PRIVATE");
            }
        }
        else if (peerId <= 0)
        {
            return Error("PEER_ID_INVALID");
        }

        bool isGroup = peerType == TLPeer.PeerType.PeerChat;
        bool includeRecent = channelId <= 0 || !broadcast;
        var perMessage = new List<(int MessageId, byte[] ReactionsBytes)>();
        var reactorIds = new HashSet<long>();
        foreach (int id in ids)
        {
            IReadOnlyCollection<TLMessageReactionInfo> rows;
            if (channelId > 0)
            {
                var saved = await _channelMessagesRepository
                    .GetMessageAsync(channelId, id);
                if (saved == null)
                {
                    continue;
                }
                saved.Value.Dispose();
                rows = await _messageReactionsRepository
                    .GetReactionsAsync(MessageReactionBox.Channel, channelId, id);
            }
            else
            {
                var saved = await _messageRepository.GetMessageAsync(userId, id);
                if (saved == null)
                {
                    continue;
                }
                saved.Value.Dispose();
                rows = await _messageReactionsRepository
                    .GetReactionsAsync(MessageReactionBox.Common, userId, id);
            }

            var entries = ReactionStore.ReadReactionEntries(rows, excludeUserId: 0);
            foreach (var entry in entries)
            {
                reactorIds.Add(entry.UserId);
            }
            bool canSeeList = isGroup || (channelId > 0 && !broadcast);
            byte[] viewBytes = ReactionStore.BuildMessageReactionsValue(entries, userId,
                includeRecent, canSeeList, includeUnread: true);
            perMessage.Add((id, viewBytes));
        }

        List<byte[]> chatBytes = channelId > 0 || isGroup
            ? await _fanout.GetChatBytesForViewerAsync(userId,
                new[] { channelId > 0 ? channelId : peerId })
            : new List<byte[]>();

        reactorIds.Add(userId);
        var resultUpdates = new Vector();
        foreach (var (id, reactionsBytes) in perMessage)
        {
            using TLUpdate update = UpdateFanout.BuildMessageReactionsUpdate(
                callerPeerBytes, id, reactionsBytes);
            resultUpdates.AppendTLObject(update.AsSpan());
        }
        var userVector = new Vector();
        _fanout.AppendUsers(ref userVector, reactorIds);
        var chatVector = new Vector();
        foreach (byte[] chatRow in chatBytes)
        {
            chatVector.AppendTLObject(chatRow);
        }

        return Ferrite.TL.baseLayer.Updates.Builder()
            .UpdatesProperty(resultUpdates)
            .Users(userVector)
            .Chats(chatVector)
            .Date((int)DateTimeOffset.Now.ToUnixTimeSeconds())
            .Seq(0)
            .Build();
    }

    private static bool IsActive(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }

    private static TLUpdates Error(string message) =>
        (TLUpdates)RpcErrorGenerator.GenerateError(400,
            System.Text.Encoding.UTF8.GetBytes(message));
}
