// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Reactions;

public sealed class GetMessageReactionsListHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IMessageReactionsRepository _messageReactionsRepository;
    private readonly IMessageRepository _messageRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateFanout _fanout;
    private readonly ILogger _log;

    public GetMessageReactionsListHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IMessageReactionsRepository messageReactionsRepository, IMessageRepository messageRepository,
        UpdateFanout fanout, ILogger log)
    {
        _authorizationRepository = authorizationRepository;
        _channelMessagesRepository = channelMessagesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _messageReactionsRepository = messageReactionsRepository;
        _messageRepository = messageRepository;

        _unitOfWork = unitOfWork;
        _fanout = fanout;
        _log = log;
    }

    [TLFunction(Constructors.baseLayer_GetMessageReactionsList)]
    public async Task<TLMessageReactionsList> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return Error("AUTH_KEY_INVALID");
        }

        long userId = auth.Value.AsAuthInfo().UserId;
        var request = (GetMessageReactionsList)q;
        long channelId = PeerResolver.ResolveInputPeerChannelId(request.Get_PeerView());
        (TLPeer.PeerType peerType, long peerId) = PeerResolver.ResolveHistoryPeer(request.Get_PeerView(), userId);
        int msgId = request.Id;
        byte[]? filter = request.Flags[0] ? request.Reaction.ToArray() : null;
        string? offset = request.Flags[1]
            ? System.Text.Encoding.UTF8.GetString(request.Offset)
            : null;
        int limit = request.Limit;

        long postAuthorId = 0;
        IReadOnlyCollection<TLMessageReactionInfo> rows;
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

            var saved = await _channelMessagesRepository
                .GetMessageAsync(channelId, msgId);
            if (saved == null)
            {
                return Error("MSG_ID_INVALID");
            }
            using (var savedMessage = saved.Value)
            {
                var original = savedMessage.AsSavedMessage().Get_OriginalMessage();
                if (original.Type == TLMessage.MessageType.Message)
                {
                    postAuthorId = ReactionStore.ResolveChannelPostAuthorId(original);
                }
            }
            rows = await _messageReactionsRepository
                .GetReactionsAsync(MessageReactionBox.Channel, channelId, msgId);
        }
        else
        {
            if (peerId <= 0)
            {
                return Error("PEER_ID_INVALID");
            }
            var saved = await _messageRepository.GetMessageAsync(userId, msgId);
            if (saved == null)
            {
                return Error("MSG_ID_INVALID");
            }
            saved.Value.Dispose();
            rows = await _messageReactionsRepository
                .GetReactionsAsync(MessageReactionBox.Common, userId, msgId);
        }

        var entries = ReactionStore.ReadReactionEntries(rows, excludeUserId: 0);
        var flattened = new List<(ReactionEntry Entry, byte[] Reaction, int Position)>();
        foreach (var entry in entries)
        {
            for (int i = 0; i < entry.Reactions.Count; i++)
            {
                byte[] reaction = entry.Reactions[i];
                if (filter != null && !filter.AsSpan().SequenceEqual(reaction))
                {
                    continue;
                }
                flattened.Add((entry, reaction, i));
            }
        }
        flattened.Sort((a, b) => a.Entry.Order != b.Entry.Order
            ? b.Entry.Order.CompareTo(a.Entry.Order)
            : b.Position.CompareTo(a.Position));

        int startIndex = 0;
        if (offset != null && TryParseOffset(offset, out long offsetOrder,
                out int offsetPosition))
        {
            while (startIndex < flattened.Count &&
                   (flattened[startIndex].Entry.Order > offsetOrder ||
                    (flattened[startIndex].Entry.Order == offsetOrder &&
                     flattened[startIndex].Position >= offsetPosition)))
            {
                startIndex++;
            }
        }

        int pageSize = limit is > 0 and <= 100 ? limit : 50;
        var page = flattened.Skip(startIndex).Take(pageSize).ToList();
        bool hasMore = startIndex + page.Count < flattened.Count;
        bool includeUnread = channelId <= 0 || userId == postAuthorId;
        List<byte[]> chatBytes = channelId > 0 || peerType == TLPeer.PeerType.PeerChat
            ? await _fanout.GetChatBytesForViewerAsync(userId,
                new[] { channelId > 0 ? channelId : peerId })
            : new List<byte[]>();

        var reactionsVector = new Vector();
        var reactorIds = new HashSet<long> { userId };
        foreach (var (entry, reaction, _) in page)
        {
            reactorIds.Add(entry.UserId);
            using TLPeer reactorPeer = new PeerUser(entry.UserId);
            var recentBuilder = MessagePeerReaction.Builder()
                .PeerId(reactorPeer.AsSpan())
                .Date(entry.Date)
                .Reaction(reaction);
            if (entry.Big)
            {
                recentBuilder = recentBuilder.Big(true);
            }
            if (includeUnread && entry.Unread)
            {
                recentBuilder = recentBuilder.Unread(true);
            }
            if (entry.UserId == userId)
            {
                recentBuilder = recentBuilder.My(true);
            }
            using var peerReaction = recentBuilder.Build();
            reactionsVector.AppendTLObject(peerReaction.ToReadOnlySpan());
        }

        var userVector = new Vector();
        _fanout.AppendUsers(ref userVector, reactorIds);
        var chatVector = new Vector();
        foreach (byte[] chatRow in chatBytes)
        {
            chatVector.AppendTLObject(chatRow);
        }

        var builder = MessageReactionsList.Builder()
            .Count(flattened.Count)
            .Reactions(reactionsVector)
            .Chats(chatVector)
            .Users(userVector);
        if (hasMore && page.Count > 0)
        {
            var last = page[^1];
            builder = builder.NextOffset(System.Text.Encoding.UTF8.GetBytes(
                $"{last.Entry.Order}_{last.Position}"));
        }

        _log.Debug($"💟 GetMessageReactionsList user:{userId} " +
                   $"peer:{(channelId > 0 ? channelId : peerId)} msg:{msgId} " +
                   $"total:{flattened.Count} page:{page.Count}");
        return builder.Build();
    }

    private static bool TryParseOffset(string offset, out long order,
        out int position)
    {
        order = 0;
        position = 0;
        string[] parts = offset.Split('_');
        return parts.Length == 2 && long.TryParse(parts[0], out order) &&
               int.TryParse(parts[1], out position);
    }

    private static bool IsActive(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }

    private static TLMessageReactionsList Error(string message) =>
        (TLMessageReactionsList)RpcErrorGenerator.GenerateError(400,
            System.Text.Encoding.UTF8.GetBytes(message));
}
