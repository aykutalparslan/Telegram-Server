// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

/// <summary>
/// Resolves a channel post's comment thread through the link carried by the
/// post and its automatically-forwarded discussion root. Discussion roots use
/// a different channel-local message id, so the forward origin is the stable
/// cross-channel identity.
/// </summary>
public sealed class GetDiscussionMessageHandler
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateFanout _fanout;

    public GetDiscussionMessageHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatRepository chatRepository,
        UpdateFanout fanout)
    {
        _chatParticipantsRepository = chatParticipantsRepository;

        _authorizationRepository = authorizationRepository;
        _channelMessagesRepository = channelMessagesRepository;
        _chatRepository = chatRepository;

        _unitOfWork = unitOfWork;
        _fanout = fanout;
    }

    [TLFunction(Constructors.baseLayer_GetDiscussionMessage)]
    public async Task<TLDiscussionMessage> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetDiscussionMessage)q;
        if (!PeerResolver.TryResolveInputPeerDialogKey(request.Get_PeerView(),
                0, out DialogPeerKey peer) ||
            peer.Type != TLPeer.PeerType.PeerChannel)
        {
            return Error("PEER_ID_INVALID");
        }
        long requestedChannelId = peer.Id;
        int requestedMessageId = request.MsgId;
        if (requestedChannelId <= 0 || requestedMessageId <= 0)
        {
            return Error("MESSAGE_ID_INVALID");
        }

        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return Error("AUTH_KEY_INVALID");
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        string? accessError = await ChannelAccess.ValidateReadAsync(_chatRepository, _chatParticipantsRepository, requestedChannelId, userId);
        if (accessError != null)
        {
            return Error(accessError);
        }

        bool requestedBroadcast;
        bool requestedMegagroup;
        using (TLChat? chat = await _chatRepository
                   .GetChatAsync(requestedChannelId))
        {
            if (chat == null || chat.Value.Type != TLChat.ChatType.Channel)
            {
                return Error("CHANNEL_INVALID");
            }
            Channel channel = chat.Value.AsChannel();
            requestedBroadcast = channel.Broadcast;
            requestedMegagroup = channel.Megagroup;
        }

        byte[]? requestedBytes = await ReadMessageAsync(requestedChannelId,
            requestedMessageId);
        if (requestedBytes == null)
        {
            return Error("MESSAGE_ID_INVALID");
        }

        long originChannelId;
        int originMessageId;
        long discussionChannelId;
        MessageSnapshot? root;
        List<MessageSnapshot> discussion;
        if (requestedBroadcast)
        {
            if (!TryReadLinkedDiscussion(requestedBytes,
                    out discussionChannelId))
            {
                return Error("MESSAGE_ID_INVALID");
            }

            string? discussionAccessError = await ChannelAccess.ValidateReadAsync(
                _chatRepository, _chatParticipantsRepository,
                discussionChannelId, userId);
            if (discussionAccessError != null)
            {
                return Error(discussionAccessError);
            }
            if (!await IsDiscussionChannelAsync(discussionChannelId))
            {
                return Error("CHANNEL_INVALID");
            }

            originChannelId = requestedChannelId;
            originMessageId = requestedMessageId;
            discussion = await ReadConversationAsync(discussionChannelId);
            root = discussion.FirstOrDefault(message =>
                TryReadForwardOrigin(message.Bytes, discussionChannelId,
                    out long channelId, out int messageId) &&
                channelId == originChannelId && messageId == originMessageId);
        }
        else if (requestedMegagroup)
        {
            discussionChannelId = requestedChannelId;
            discussion = await ReadConversationAsync(discussionChannelId);
            int rootId = ResolveThreadRootId(requestedBytes, requestedMessageId);
            root = discussion.FirstOrDefault(message => message.Id == rootId);
            if (root == null ||
                !TryReadForwardOrigin(root.Bytes, discussionChannelId,
                    out originChannelId, out originMessageId))
            {
                return Error("MESSAGE_ID_INVALID");
            }

            byte[]? originBytes = await ReadMessageAsync(originChannelId,
                originMessageId);
            if (originBytes == null ||
                !TryReadLinkedDiscussion(originBytes, out long linkedChannelId) ||
                linkedChannelId != discussionChannelId ||
                !await IsBroadcastChannelAsync(originChannelId))
            {
                return Error("MESSAGE_ID_INVALID");
            }
        }
        else
        {
            return Error("MESSAGE_ID_INVALID");
        }

        if (root == null)
        {
            return Error("MESSAGE_ID_INVALID");
        }

        List<MessageSnapshot> replies = discussion
            .Where(message => message.Id != root.Id &&
                              ResolveReplyRootId(message.Bytes) == root.Id)
            .OrderByDescending(message => message.Id)
            .ToList();
        var selected = new List<MessageSnapshot>(replies.Count + 1);
        selected.AddRange(replies);
        selected.Add(root);

        int readInboxMaxId = 0;
        int readOutboxMaxId = 0;
        using (TLChannelReadState? readState = await _channelMessagesRepository.GetReadStateAsync(userId,
                       discussionChannelId))
        {
            if (readState != null)
            {
                ChannelReadState state = readState.Value.AsChannelReadState();
                readInboxMaxId = state.ReadInboxMaxId;
                readOutboxMaxId = state.ReadOutboxMaxId;
            }
        }

        int unreadCount = replies.Count(message =>
            message.Id > readInboxMaxId &&
            ResolveSenderUserId(message.Bytes) != userId);
        int maxId = selected.Max(message => message.Id);

        var relatedUserIds = new HashSet<long>();
        var relatedChatIds = new HashSet<long>
        {
            originChannelId,
            discussionChannelId,
        };
        foreach (MessageSnapshot snapshot in selected)
        {
            using var message = new TLMessage(snapshot.Bytes, 0,
                snapshot.Bytes.Length);
            MessageStore.AddMessageRelatedPeers(message, relatedUserIds,
                relatedChatIds);
        }
        List<byte[]> chatBytes = await _fanout.GetChatBytesForViewerAsync(userId,
            relatedChatIds);

        var messages = new Vector();
        foreach (MessageSnapshot snapshot in selected)
        {
            messages.AppendTLObject(snapshot.Bytes);
        }
        var chats = new Vector();
        foreach (byte[] bytes in chatBytes)
        {
            chats.AppendTLObject(bytes);
        }
        var users = new Vector();
        _fanout.AppendUsers(ref users, relatedUserIds);

        var builder = DiscussionMessage.Builder()
            .Messages(messages)
            .MaxId(maxId)
            .UnreadCount(unreadCount)
            .Chats(chats)
            .Users(users);
        if (readInboxMaxId > 0)
        {
            builder = builder.ReadInboxMaxId(readInboxMaxId);
        }
        if (readOutboxMaxId > 0)
        {
            builder = builder.ReadOutboxMaxId(readOutboxMaxId);
        }
        return builder.Build();
    }

    private async Task<byte[]?> ReadMessageAsync(long channelId, int messageId)
    {
        using TLSavedMessage? saved = await _channelMessagesRepository
            .GetMessageAsync(channelId, messageId);
        return saved?.AsSavedMessage().Get_OriginalMessage().AsSpan().ToArray();
    }

    private async Task<List<MessageSnapshot>> ReadConversationAsync(long channelId)
    {
        IReadOnlyCollection<TLSavedMessage> stored = await _channelMessagesRepository.GetMessagesAsync(channelId);
        var messages = new List<MessageSnapshot>();
        foreach (TLSavedMessage row in stored)
        {
            using var saved = row;
            TLMessage message = saved.AsSavedMessage().Get_OriginalMessage();
            if (!MessageStore.TryReadStoredMessageInfo(message,
                    out StoredMessageInfo info) ||
                info.PeerType != TLPeer.PeerType.PeerChannel ||
                info.PeerId != channelId)
            {
                continue;
            }
            messages.Add(new MessageSnapshot(info.Id, info.Date, info.Bytes));
        }
        return messages.OrderByDescending(message => message.Id).ToList();
    }

    private async Task<bool> IsDiscussionChannelAsync(long channelId)
    {
        using TLChat? chat = await _chatRepository.GetChatAsync(channelId);
        return chat != null && chat.Value.Type == TLChat.ChatType.Channel &&
               chat.Value.AsChannel().Megagroup &&
               !chat.Value.AsChannel().Broadcast;
    }

    private async Task<bool> IsBroadcastChannelAsync(long channelId)
    {
        using TLChat? chat = await _chatRepository.GetChatAsync(channelId);
        return chat != null && chat.Value.Type == TLChat.ChatType.Channel &&
               chat.Value.AsChannel().Broadcast;
    }

    private static bool TryReadLinkedDiscussion(byte[] messageBytes,
        out long discussionChannelId)
    {
        using var stored = new TLMessage(messageBytes, 0, messageBytes.Length);
        if (stored.Type == TLMessage.MessageType.Message)
        {
            Message message = stored.AsMessage();
            if (message.Flags[23] &&
                message.Get_RepliesView().Is(out MessageReplies replies) &&
                replies.Comments && replies.ChannelId > 0)
            {
                discussionChannelId = replies.ChannelId;
                return true;
            }
        }
        discussionChannelId = 0;
        return false;
    }

    private static bool TryReadForwardOrigin(byte[] messageBytes,
        long discussionChannelId, out long originChannelId,
        out int originMessageId)
    {
        using var stored = new TLMessage(messageBytes, 0, messageBytes.Length);
        if (stored.Type == TLMessage.MessageType.Message)
        {
            Message message = stored.AsMessage();
            if (message.Flags[2] && message.Flags[8] &&
                message.Get_PeerIdView().Is(out PeerChannel peer) &&
                peer.ChannelId == discussionChannelId &&
                message.Get_FromIdView().Is(out PeerChannel sender) &&
                message.Get_FwdFromView().Is(out MessageFwdHeader forward) &&
                forward.Flags[0] && forward.Flags[2] &&
                forward.Get_FromIdView().Is(out PeerChannel origin) &&
                sender.ChannelId == origin.ChannelId &&
                origin.ChannelId != discussionChannelId &&
                forward.ChannelPost > 0)
            {
                originChannelId = origin.ChannelId;
                originMessageId = forward.ChannelPost;
                return true;
            }
        }
        originChannelId = 0;
        originMessageId = 0;
        return false;
    }

    private static int ResolveThreadRootId(byte[] messageBytes, int messageId)
    {
        int replyRoot = ResolveReplyRootId(messageBytes);
        return replyRoot > 0 ? replyRoot : messageId;
    }

    private static int ResolveReplyRootId(byte[] messageBytes)
    {
        using var stored = new TLMessage(messageBytes, 0, messageBytes.Length);
        MessageReplyHeaderView reply;
        if (stored.Type == TLMessage.MessageType.Message)
        {
            Message message = stored.AsMessage();
            if (!message.Flags[3]) return 0;
            reply = message.Get_ReplyToView();
        }
        else if (stored.Type == TLMessage.MessageType.MessageService)
        {
            MessageService message = stored.AsMessageService();
            if (!message.Flags[3]) return 0;
            reply = message.Get_ReplyToView();
        }
        else
        {
            return 0;
        }

        if (!reply.Is(out MessageReplyHeader header)) return 0;
        return header.Flags[1] && header.ReplyToTopId > 0
            ? header.ReplyToTopId
            : header.ReplyToMsgId;
    }

    private static long ResolveSenderUserId(byte[] messageBytes)
    {
        using var stored = new TLMessage(messageBytes, 0, messageBytes.Length);
        if (stored.Type == TLMessage.MessageType.Message)
        {
            Message message = stored.AsMessage();
            return message.Flags[8] &&
                   message.Get_FromIdView().Is(out PeerUser user)
                ? user.UserId
                : 0;
        }
        if (stored.Type == TLMessage.MessageType.MessageService)
        {
            MessageService message = stored.AsMessageService();
            return message.Flags[8] &&
                   message.Get_FromIdView().Is(out PeerUser user)
                ? user.UserId
                : 0;
        }
        return 0;
    }

    private static TLDiscussionMessage Error(string message) =>
        (TLDiscussionMessage)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
