// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

// Await-safe snapshot of a message-box write. The serialized message remains valid
// after the pooled builder used for the repository write has been disposed.
public readonly record struct StoredMessageWrite(int Id, int Pts, byte[] Bytes);

// Await-safe projection used by history, dialogs, deletion, pinning, and forums.
public readonly record struct StoredMessageInfo(
    int Id,
    int Date,
    TLPeer.PeerType PeerType,
    long PeerId,
    byte[] Bytes);

// Persistence-only boundary for common message boxes and their logical-copy rows.
// Search indexing, update fan-out, logging, and unit-of-work commits stay with the
// calling operation so their externally observable ordering remains unchanged.
public sealed class MessageStore
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IMessageReactionsRepository _messageReactionsRepository;
    private readonly IMessageRepository _messageRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly IdAllocators _ids;

    public MessageStore(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IMessageReactionsRepository messageReactionsRepository, IMessageRepository messageRepository,
        IUpdatesContextFactory updatesContextFactory, IdAllocators ids)
    {
        _channelMessagesRepository = channelMessagesRepository;
        _messageReactionsRepository = messageReactionsRepository;
        _messageRepository = messageRepository;

        _unitOfWork = unitOfWork;
        _updatesContextFactory = updatesContextFactory;
        _ids = ids;
    }

    public async Task<(int PreviousPts, int Pts)> PutOutgoingMessageAsync(
        IUpdatesContext senderContext, long ownerId, TLMessage outgoingMessage)
    {
        int previousPts = await senderContext.Pts();
        int pts = await senderContext.IncrementPts();
        _messageRepository.PutMessage(ownerId, outgoingMessage, pts);
        return (previousPts, pts);
    }

    public async ValueTask<long> CreateMessageCopyAsync(long ownerId, int messageId)
    {
        long logicalId = await _ids.NextLogicalMessageIdAsync();
        PutMessageCopy(logicalId, ownerId, messageId);
        return logicalId;
    }

    public void PutMessageCopy(long logicalId, long ownerId, int messageId)
    {
        using TLMessageCopyInfo copy = MessageCopyInfo.Builder()
            .LogicalId(logicalId)
            .UserId(ownerId)
            .MessageId(messageId)
            .Build();
        _messageReactionsRepository.PutMessageCopy(copy);
    }

    public async Task<StoredMessageWrite> PutIncomingMessageAsync(long recipientId,
        TLMessage outgoingMessage, TLPeer from, long logicalId)
    {
        var receiverContext = _updatesContextFactory.GetUpdatesContext(null, recipientId);
        int receiverMessageId = await receiverContext.NextMessageId();
        PutMessageCopy(logicalId, recipientId, receiverMessageId);
        // `from_scheduled` reports to the SENDER that their own queued message was
        // flushed (/api/scheduled-messages); an incoming copy is never from the
        // recipient's queue, and the pinned client reads the flag as a reason to
        // notify about an outgoing message (`MessagesManager.cpp:8900`).
        using TLMessage incomingMessage = outgoingMessage.AsMessage().Clone()
            .Id(receiverMessageId)
            .OutProperty(false)
            .FromScheduled(false)
            .PeerId(from.AsSpan())
            .Build();

        int pts = await receiverContext.IncrementPtsForMessage(
            (int)from.Type, GetPeerId(from), receiverMessageId);
        _messageRepository.PutMessage(recipientId, incomingMessage, pts);
        return new StoredMessageWrite(receiverMessageId, pts,
            incomingMessage.AsSpan().ToArray());
    }

    /// <summary>
    /// A mention is per recipient, so only the named participant's copy carries
    /// `mentioned`. `media_unread` is co-set because the pinned client counts a
    /// mention as unread only while both flags are present
    /// (`MessagesManager.cpp:11194`); `messages.readMentions` clears `mentioned`
    /// alone so unread voice content survives being caught up on.
    /// </summary>
    public async Task<StoredMessageWrite> PutIncomingGroupMessageAsync(long participantId,
        long chatId, TLMessage outgoingMessage, long logicalId,
        bool mentioned = false)
    {
        var receiverContext = _updatesContextFactory.GetUpdatesContext(null, participantId);
        int receiverMessageId = await receiverContext.NextMessageId();
        PutMessageCopy(logicalId, participantId, receiverMessageId);
        using TLMessage incomingMessage = outgoingMessage.AsMessage().Clone()
            .Id(receiverMessageId)
            .OutProperty(false)
            .FromScheduled(false)
            .Mentioned(mentioned)
            .MediaUnread(mentioned || outgoingMessage.AsMessage().MediaUnread)
            .Build();

        int pts = await receiverContext.IncrementPtsForMessage(
            (int)TLPeer.PeerType.PeerChat, chatId, receiverMessageId);
        _messageRepository.PutMessage(participantId, incomingMessage, pts);
        return new StoredMessageWrite(receiverMessageId, pts,
            incomingMessage.AsSpan().ToArray());
    }

    /// <summary>
    /// Writes one private-peer service message into a single user's message box
    /// with the correct out/in perspective and pts, returning the stored id,
    /// pts, and serialized bytes. Used by the 1:1 call log; the caller owns the
    /// live update fan-out.
    /// </summary>
    public async Task<StoredMessageWrite> PutPrivateServiceMessageAsync(long ownerId,
        long? authKeyId, long dialogPeerUserId, long fromUserId, bool outgoing,
        byte[] actionBytes, int date, byte[]? replyToHeaderBytes = null)
    {
        var context = _updatesContextFactory.GetUpdatesContext(authKeyId, ownerId);
        int messageId = await context.NextMessageId();
        using TLPeer fromPeer = new PeerUser(fromUserId);
        using TLPeer peerId = new PeerUser(dialogPeerUserId);
        var builder = MessageService.Builder()
            .Id(messageId)
            .OutProperty(outgoing)
            .FromId(fromPeer.AsSpan())
            .PeerId(peerId.AsSpan())
            .Date(date)
            .Action(actionBytes);
        if (replyToHeaderBytes is { Length: > 0 })
        {
            builder = builder.ReplyTo(replyToHeaderBytes);
        }
        using TLMessage serviceMessage = builder.Build();
        int pts = outgoing
            ? await context.IncrementPts()
            : await context.IncrementPtsForMessage(
                (int)TLPeer.PeerType.PeerUser, dialogPeerUserId, messageId);
        _messageRepository.PutMessage(ownerId, serviceMessage, pts);
        return new StoredMessageWrite(messageId, pts,
            serviceMessage.AsSpan().ToArray());
    }

    /// <summary>
    /// Writes an ordinary outgoing media message into a user's self dialog. A
    /// completed group-call recording is private to the account that started it,
    /// so both peer ids name that account and the caller owns the later commit
    /// and live update fan-out.
    /// </summary>
    public async Task<StoredMessageWrite> PutSelfMediaMessageAsync(long ownerId,
        byte[] mediaBytes, byte[] messageBytes, int date)
    {
        var context = _updatesContextFactory.GetUpdatesContext(null, ownerId);
        int messageId = await context.NextMessageId();
        using TLPeer self = new PeerUser(ownerId);
        using TLMessage message = Message.Builder()
            .Id(messageId)
            .OutProperty(true)
            .FromId(self.AsSpan())
            .PeerId(self.AsSpan())
            .MessageProperty(messageBytes)
            .Date(date)
            .Media(mediaBytes)
            .Build();
        int pts = await context.IncrementPts();
        _messageRepository.PutMessage(ownerId, message, pts);
        return new StoredMessageWrite(messageId, pts,
            message.AsSpan().ToArray());
    }

    /// <summary>
    /// Writes one basic-group service message into a single member's box. Basic
    /// groups fan out per-member copies, so each member gets its own id and pts,
    /// and only the actor's copy is outgoing. The caller loops over members, owns
    /// the SaveAsync, and owns the live update fan-out.
    /// </summary>
    public async Task<StoredMessageWrite> PutBasicGroupServiceMessageAsync(long ownerId,
        long? authKeyId, long chatId, long fromUserId, byte[] actionBytes, int date)
    {
        bool outgoing = ownerId == fromUserId;
        var context = _updatesContextFactory.GetUpdatesContext(
            outgoing ? authKeyId : null, ownerId);
        int messageId = await context.NextMessageId();
        using TLPeer fromPeer = new PeerUser(fromUserId);
        using TLPeer chatPeer = new PeerChat(chatId);
        using TLMessage serviceMessage = MessageService.Builder()
            .Id(messageId)
            .OutProperty(outgoing)
            .FromId(fromPeer.AsSpan())
            .PeerId(chatPeer.AsSpan())
            .Date(date)
            .Action(actionBytes)
            .Build();
        int pts = outgoing
            ? await context.IncrementPts()
            : await context.IncrementPtsForMessage(
                (int)TLPeer.PeerType.PeerChat, chatId, messageId);
        _messageRepository.PutMessage(ownerId, serviceMessage, pts);
        return new StoredMessageWrite(messageId, pts, serviceMessage.AsSpan().ToArray());
    }

    /// <summary>
    /// Writes one channel service message into the single per-channel box: next
    /// channel message id, stored messageService row, advanced channel pts. Static
    /// so both the channels handler base and the group-call action writer share one
    /// implementation without threading a MessageStore through every subclass.
    /// The caller owns the SaveAsync and the fan-out.
    /// </summary>
    public static async Task<StoredMessageWrite> PutChannelServiceMessageAsync(
        IChannelMessagesRepository channelMessagesRepository,
        ICounterFactory counterFactory, long channelId,
        long actorUserId, byte[] actionBytes, int date,
        byte[]? replyToHeaderBytes = null)
    {
        var channelBox = new ChannelMessageBox(counterFactory, channelId);
        int messageId = await channelBox.NextMessageId();
        using TLPeer channelPeer = new PeerChannel(channelId);
        using TLPeer actorPeer = new PeerUser(actorUserId);
        var builder = MessageService.Builder()
            .Id(messageId)
            .FromId(actorPeer.AsSpan())
            .PeerId(channelPeer.AsSpan())
            .Date(date)
            .Action(actionBytes);
        if (replyToHeaderBytes is { Length: > 0 })
        {
            builder = builder.ReplyTo(replyToHeaderBytes);
        }
        using TLMessage serviceMessage = builder.Build();
        int pts = await channelBox.IncrementPts();
        channelMessagesRepository.PutMessage(channelId, serviceMessage, pts);
        return new StoredMessageWrite(messageId, pts, serviceMessage.AsSpan().ToArray());
    }

    public void DeleteMessages(long ownerId, IEnumerable<int> messageIds)
    {
        foreach (int messageId in messageIds)
        {
            _messageRepository.DeleteMessage(ownerId, messageId);
        }
    }

    public async Task<List<int>> DeleteConversationAsync(long ownerId,
        TLPeer.PeerType peerType, long peerId, int maxId,
        int? minDate, int? maxDate)
    {
        var saved = await _messageRepository.GetMessagesAsync(ownerId);
        var deletedIds = new List<int>();
        foreach (var row in saved)
        {
            using var savedMessage = row;
            var message = savedMessage.AsSavedMessage().Get_OriginalMessage();
            if (!TryReadStoredMessageInfo(message, out var info) ||
                info.PeerType != peerType ||
                info.PeerId != peerId)
            {
                continue;
            }
            if (maxId > 0 && info.Id > maxId) continue;
            if (minDate.HasValue && info.Date < minDate.Value) continue;
            if (maxDate.HasValue && info.Date > maxDate.Value) continue;
            deletedIds.Add(info.Id);
        }

        DeleteMessages(ownerId, deletedIds);
        return deletedIds;
    }

    public static bool TryReadStoredMessageInfo(TLMessage message,
        out StoredMessageInfo info)
    {
        if (message.Type == TLMessage.MessageType.Message)
        {
            var messageBody = message.AsMessage();
            // Group/channel messages key on the dialog peer for both directions;
            // the from-id fallback below is only for incoming private messages.
            if (TryReadPeer(messageBody.Get_PeerIdView(), out var dialogPeer) &&
                dialogPeer.Type is TLPeer.PeerType.PeerChat or TLPeer.PeerType.PeerChannel)
            {
                info = new StoredMessageInfo(messageBody.Id, messageBody.Date,
                    dialogPeer.Type, dialogPeer.Id, message.AsSpan().ToArray());
                return true;
            }
            if (messageBody.OutProperty &&
                TryReadPeer(messageBody.Get_PeerIdView(), out var outPeer))
            {
                info = new StoredMessageInfo(messageBody.Id, messageBody.Date,
                    outPeer.Type, outPeer.Id, message.AsSpan().ToArray());
                return true;
            }
            if (messageBody.Flags[8] &&
                TryReadPeer(messageBody.Get_FromIdView(), out var fromPeer))
            {
                info = new StoredMessageInfo(messageBody.Id, messageBody.Date,
                    fromPeer.Type, fromPeer.Id, message.AsSpan().ToArray());
                return true;
            }
            if (TryReadPeer(messageBody.Get_PeerIdView(), out var peer))
            {
                info = new StoredMessageInfo(messageBody.Id, messageBody.Date,
                    peer.Type, peer.Id, message.AsSpan().ToArray());
                return true;
            }
        }
        else if (message.Type == TLMessage.MessageType.MessageService)
        {
            var service = message.AsMessageService();
            if (TryReadPeer(service.Get_PeerIdView(), out var peer))
            {
                if (peer.Type is TLPeer.PeerType.PeerChat or TLPeer.PeerType.PeerChannel ||
                    service.OutProperty || !service.Flags[8])
                {
                    info = new StoredMessageInfo(service.Id, service.Date,
                        peer.Type, peer.Id, message.AsSpan().ToArray());
                    return true;
                }
            }
            if (service.Flags[8] &&
                TryReadPeer(service.Get_FromIdView(), out var fromPeer))
            {
                info = new StoredMessageInfo(service.Id, service.Date,
                    fromPeer.Type, fromPeer.Id, message.AsSpan().ToArray());
                return true;
            }
        }

        info = default;
        return false;
    }

    public static void AddMessageRelatedPeers(TLMessage message,
        HashSet<long> userIds, HashSet<long> chatIds)
    {
        if (!TryReadStoredMessageInfo(message, out StoredMessageInfo info)) return;
        if (info.PeerType == TLPeer.PeerType.PeerUser) userIds.Add(info.PeerId);
        else if (info.PeerType is TLPeer.PeerType.PeerChat or TLPeer.PeerType.PeerChannel)
            chatIds.Add(info.PeerId);

        if (message.Type == TLMessage.MessageType.Message)
        {
            var regular = message.AsMessage();
            if (regular.Flags[8] &&
                PeerResolver.TryReadPeer(regular.Get_FromIdView(), out var from))
            {
                if (from.Type == TLPeer.PeerType.PeerUser)
                {
                    userIds.Add(from.Id);
                }
                else
                {
                    chatIds.Add(from.Id);
                }
            }
            return;
        }
        if (message.Type != TLMessage.MessageType.MessageService) return;
        var service = message.AsMessageService();
        if (service.Flags[8] &&
            PeerResolver.TryReadPeer(service.Get_FromIdView(), out var serviceFrom))
        {
            if (serviceFrom.Type == TLPeer.PeerType.PeerUser)
            {
                userIds.Add(serviceFrom.Id);
            }
            else
            {
                chatIds.Add(serviceFrom.Id);
            }
        }
        MessageActionView action = service.Get_ActionView();
        foreach (long userId in ReadActionUserIds(action)) userIds.Add(userId);
        foreach (long chatId in ReadActionChatIds(action)) chatIds.Add(chatId);
    }

    private static List<long> ReadActionUserIds(MessageActionView action)
    {
        var result = new List<long>();
        if (action.Is(out MessageActionChatCreate create))
        {
            var users = create.Users;
            for (int i = 0; i < users.Count; i++) result.Add(users[i]);
        }
        else if (action.Is(out MessageActionChatAddUser add))
        {
            var users = add.Users;
            for (int i = 0; i < users.Count; i++) result.Add(users[i]);
        }
        else if (action.Is(out MessageActionChatDeleteUser deleteUser))
            result.Add(deleteUser.UserId);
        else if (action.Is(out MessageActionChatJoinedByLink joined))
            result.Add(joined.InviterId);
        else if (action.Is(out MessageActionInviteToGroupCall groupCallInvite))
        {
            VectorOfLong users = groupCallInvite.Users;
            for (int i = 0; i < users.Count; i++) result.Add(users[i]);
        }
        return result;
    }

    private static List<long> ReadActionChatIds(MessageActionView action)
    {
        var result = new List<long>();
        if (action.Is(out MessageActionChatMigrateTo migrateTo))
            result.Add(migrateTo.ChannelId);
        else if (action.Is(out MessageActionChannelMigrateFrom migrateFrom))
            result.Add(migrateFrom.ChatId);
        return result;
    }

    private static bool TryReadPeer(PeerView peer,
        out (TLPeer.PeerType Type, long Id) value)
    {
        if (peer.Is(out PeerUser user))
        {
            value = (TLPeer.PeerType.PeerUser, user.UserId);
            return true;
        }
        if (peer.Is(out PeerChat chat))
        {
            value = (TLPeer.PeerType.PeerChat, chat.ChatId);
            return true;
        }
        if (peer.Is(out PeerChannel channel))
        {
            value = (TLPeer.PeerType.PeerChannel, channel.ChannelId);
            return true;
        }

        value = default;
        return false;
    }

    private static long GetPeerId(TLPeer peer) => peer.Type switch
    {
        TLPeer.PeerType.PeerUser => peer.AsPeerUser().UserId,
        TLPeer.PeerType.PeerChat => peer.AsPeerChat().ChatId,
        TLPeer.PeerType.PeerChannel => peer.AsPeerChannel().ChannelId,
        _ => 0
    };
}
