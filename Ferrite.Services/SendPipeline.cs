// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services;

public sealed record PipelineResult<T>(T? Value, string? Error) where T : class
{
    public static PipelineResult<T> Success(T value) => new(value, null);
    public static PipelineResult<T> Failure(string error) => new(null, error);
}

public sealed record ShortSentBatch(long UserId, TLPeer.PeerType PeerType,
    long PeerId, long RandomId, int Id, int Pts, int Date, byte[] MessageBytes,
    byte[]? ChatBytes = null);

public sealed record ChannelSentBatch(long UserId, long ChannelId, long RandomId,
    int Id, int Pts, int Date, byte[] MessageBytes, byte[] ChannelBytes);

public sealed record MediaSentBatch(long UserId, TLPeer.PeerType PeerType,
    long PeerId, long RandomId, int Id, int Pts, int Date, byte[] MessageBytes,
    byte[]? ChatBytes);

public sealed record ReactionCallerBatch(long UserId, byte[] PeerBytes, int MsgId,
    byte[] ReactionsBytes, List<ReactionEntry> Entries, long ReactionConfigChatId);

// Stateful orchestration for message/reaction persistence and indexing. Live peer
// delivery and caller-facing Updates hydration are delegated to UpdateFanout.
public sealed class SendPipeline
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IForumTopicsRepository _forumTopicsRepository;
    private readonly IMessageReactionsRepository _messageReactionsRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IUserRepository _userRepository;

    private readonly record struct PendingDelivery(long RecipientId,
        byte[] MessageBytes, int Pts, MessageSearchModel SearchModel);

    private readonly IUnitOfWork _unitOfWork;
    private readonly MessageStore _messages;
    private readonly ISearchEngine _search;
    private readonly UpdateFanout _fanout;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly ICounterFactory _counterFactory;
    private readonly MessageExpiryStore _expiry;
    private readonly ILogger _log;

    public SendPipeline(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IForumTopicsRepository forumTopicsRepository, IMessageReactionsRepository messageReactionsRepository, IMessageRepository messageRepository, IUserRepository userRepository, MessageStore messages,
        ISearchEngine search, UpdateFanout fanout,
        IUpdatesContextFactory updatesContextFactory, ICounterFactory counterFactory,
        MessageExpiryStore expiry, ILogger log)
    {
        _channelMessagesRepository = channelMessagesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _forumTopicsRepository = forumTopicsRepository;
        _messageReactionsRepository = messageReactionsRepository;
        _messageRepository = messageRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _messages = messages;
        _search = search;
        _fanout = fanout;
        _updatesContextFactory = updatesContextFactory;
        _counterFactory = counterFactory;
        _expiry = expiry;
        _log = log;
    }

    public async Task<ShortSentBatch> SendPrivateMessageAsync(long authKeyId,
        long userId, TLPeer.PeerType peerType, long peerId, byte[] requestBytes,
        byte[]? media = null, long groupedId = 0)
    {
        var senderContext = _updatesContextFactory.GetUpdatesContext(authKeyId, userId);
        int senderMessageId = (int)await senderContext.NextMessageId();
        int ttlPeriod = await _expiry.ResolveTtlPeriodAsync(userId, peerType, peerId);
        using TLPeer from = new PeerUser(userId);
        using TLPeer to = PeerResolver.BuildPeer(peerType, peerId);
        using var request = new TLBytes(requestBytes, 0, requestBytes.Length);
        long randomId = ((SendMessage)request).RandomId;
        using TLMessage outgoingMessage = GenerateOutgoingMessage(request, senderMessageId,
            from, to, UnixNow(), media: media, groupedId: groupedId,
            ttlPeriod: ttlPeriod);

        int pts = await PutOutgoingAsync(senderContext, userId, outgoingMessage,
            from, to);
        TrackExpiry(MessageExpiryBox.Common, userId, outgoingMessage, ttlPeriod);
        long logicalId = await _messages.CreateMessageCopyAsync(userId, senderMessageId);
        PendingDelivery? pending = null;
        if (peerId != userId)
        {
            pending = await PutIncomingAsync(peerId, outgoingMessage, from, to, logicalId,
                ttlPeriod);
        }

        await _unitOfWork.SaveAsync();
        if (pending != null)
        {
            await DeliverAsync(pending.Value);
        }
        await IndexOutgoingAsync(userId, outgoingMessage, from, to);
        if (pending != null)
        {
            await IndexIncomingAsync(pending.Value);
        }
        return new ShortSentBatch(userId, peerType, peerId, randomId,
            senderMessageId, pts, outgoingMessage.AsMessage().Date,
            outgoingMessage.AsSpan().ToArray());
    }

    public async Task<ShortSentBatch> SendBasicGroupMessageAsync(long authKeyId,
        long userId, long chatId, IReadOnlyCollection<long> participantIds,
        byte[] requestBytes, byte[]? media = null, long groupedId = 0,
        byte[]? chatBytes = null)
    {
        var senderContext = _updatesContextFactory.GetUpdatesContext(authKeyId, userId);
        int senderMessageId = (int)await senderContext.NextMessageId();
        MentionPlan mentions = ResolveMentions(requestBytes);
        int ttlPeriod = await _expiry.ResolveTtlPeriodAsync(userId,
            TLPeer.PeerType.PeerChat, chatId);
        using TLPeer from = new PeerUser(userId);
        using TLPeer to = new PeerChat(chatId);
        using var request = new TLBytes(requestBytes, 0, requestBytes.Length);
        long randomId = ((SendMessage)request).RandomId;
        using TLMessage outgoingMessage = GenerateOutgoingMessage(request, senderMessageId,
            from, to, UnixNow(), media: media, groupedId: groupedId,
            entitiesOverride: mentions.EntitiesBytes, ttlPeriod: ttlPeriod);
        int senderPts = await PutOutgoingAsync(senderContext, userId,
            outgoingMessage, from, to);
        TrackExpiry(MessageExpiryBox.Common, userId, outgoingMessage, ttlPeriod);
        long logicalId = await _messages.CreateMessageCopyAsync(userId, senderMessageId);
        var pending = new List<PendingDelivery>();

        foreach (long participantId in participantIds)
        {
            if (participantId == userId)
            {
                continue;
            }

            pending.Add(await PutIncomingGroupAsync(participantId, chatId,
                outgoingMessage, from, logicalId,
                mentions.UserIds.Contains(participantId), ttlPeriod));
        }

        await _unitOfWork.SaveAsync();
        foreach (PendingDelivery delivery in pending)
        {
            await DeliverAsync(delivery);
        }
        await IndexOutgoingAsync(userId, outgoingMessage, from, to);
        foreach (PendingDelivery delivery in pending)
        {
            await IndexIncomingAsync(delivery);
        }
        _log.Debug($"💬 Group message was sent Sender: {userId} Chat: {chatId} " +
                   $"Participants: {participantIds.Count} PTS: {senderPts}");
        return new ShortSentBatch(userId, TLPeer.PeerType.PeerChat, chatId,
            randomId, senderMessageId, senderPts, outgoingMessage.AsMessage().Date,
            outgoingMessage.AsSpan().ToArray(), chatBytes);
    }

    public async Task<ChannelSentBatch> SendChannelMessageAsync(long userId,
        long channelId, DialogPeerKey sender, bool broadcast, int forumTopicId,
        StoredMessageForumTopic? forumTopic, byte[] requestBytes, byte[] channelBytes,
        byte[]? media = null, long groupedId = 0)
    {
        using var request = new TLBytes(requestBytes, 0, requestBytes.Length);
        long randomId = ((SendMessage)request).RandomId;
        var channelBox = new ChannelMessageBox(_counterFactory, channelId);
        int messageId = await channelBox.NextMessageId();
        // A broadcast post is addressed to subscribers, not to a participant, so
        // the pinned client never counts one as an unread mention.
        MentionPlan mentions = broadcast
            ? MentionPlan.None
            : ResolveMentions(requestBytes);
        int ttlPeriod = await _expiry.ResolveTtlPeriodAsync(userId,
            TLPeer.PeerType.PeerChannel, channelId);
        using TLPeer from = PeerResolver.BuildPeer(sender.Type, sender.Id);
        using TLPeer channelPeer = new PeerChannel(channelId);
        using TLMessage message = GenerateOutgoingMessage(request, messageId, from,
            channelPeer, UnixNow(), outgoing: false, post: broadcast,
            forumTopic: forumTopic != null && forumTopicId != 1, media: media,
            groupedId: groupedId, entitiesOverride: mentions.EntitiesBytes,
            ttlPeriod: ttlPeriod);
        byte[] messageBytes = message.AsSpan().ToArray();
        var memberIds = await _fanout.GetOtherActiveChannelMemberIdsAsync(channelId, userId);
        await channelBox.BeginPtsPublication();
        int pts;
        try
        {
            pts = await channelBox.IncrementPts();
            _channelMessagesRepository.PutMessage(channelId, message, pts);
            TrackExpiry(MessageExpiryBox.Channel, channelId, message, ttlPeriod);

            if (forumTopic != null)
            {
                using TLForumTopicInfo updatedTopic = ForumMessages.BuildStoredForumTopic(
                    forumTopic with { TopMessage = messageId });
                _forumTopicsRepository.PutTopic(updatedTopic);
                await UpdateForumTopicUnreadStateAsync(channelId, forumTopicId, userId,
                    messageId);
            }

            // Redis counters are immediately visible. Commit the Cassandra row before
            // indexing or fan-out so a concurrent difference/history read can never
            // advance through a PTS whose message is still only queued locally.
            await _unitOfWork.SaveAsync();

            // A channel post exists once, so the shared row never carries `mentioned`;
            // only the delivery to a named member does.
            byte[]? mentionedBytes = mentions.UserIds.Count == 0
                ? null
                : MessageMentions.StampUnread(messageBytes);
            foreach (long memberId in memberIds)
            {
                await _fanout.EnqueueNewChannelMessageAsync(memberId,
                    mentionedBytes != null && mentions.UserIds.Contains(memberId)
                        ? mentionedBytes
                        : messageBytes,
                    pts);
            }
        }
        finally
        {
            await channelBox.CompletePtsPublication();
        }

        var searchModel = new MessageSearchModel(
            channelId + "_" + messageId, channelId,
            (int)from.Type, GetPeerId(from),
            (int)TLPeer.PeerType.PeerChannel, channelId, messageId,
            null, Encoding.UTF8.GetString(message.AsMessage().MessageProperty),
            message.AsMessage().Date);
        await _search.IndexMessage(searchModel);

        int date = message.AsMessage().Date;
        _log.Debug($"📣 Channel message sent sender:{userId} channel:{channelId} " +
                   $"broadcast:{broadcast} id:{messageId} pts:{pts} members:{memberIds.Count}");
        return new ChannelSentBatch(userId, channelId, randomId, messageId, pts, date,
            messageBytes, channelBytes);
    }

    /// <summary>
    /// Persists an already assembled outgoing message instead of generating one
    /// from a `messages.sendMessage` request. Forwarding copies source content
    /// verbatim, so the caller owns the row's shape; only the box-local id is
    /// assigned here. The rest of the write, copy mapping, pts, indexing, and
    /// live delivery is the ordinary send path.
    /// </summary>
    public async Task<ShortSentBatch> SendPreparedPrivateMessageAsync(long authKeyId,
        long userId, TLPeer.PeerType peerType, long peerId, byte[] templateBytes,
        long randomId)
    {
        var senderContext = _updatesContextFactory.GetUpdatesContext(authKeyId, userId);
        int senderMessageId = (int)await senderContext.NextMessageId();
        int ttlPeriod = await _expiry.ResolveTtlPeriodAsync(userId, peerType, peerId);
        using TLPeer from = new PeerUser(userId);
        using TLPeer to = PeerResolver.BuildPeer(peerType, peerId);
        using TLMessage outgoingMessage = WithLocalId(templateBytes, senderMessageId,
            ttlPeriod: ttlPeriod);

        int pts = await PutOutgoingAsync(senderContext, userId, outgoingMessage,
            from, to);
        TrackExpiry(MessageExpiryBox.Common, userId, outgoingMessage, ttlPeriod);
        long logicalId = await _messages.CreateMessageCopyAsync(userId, senderMessageId);
        PendingDelivery? pending = null;
        if (peerId != userId)
        {
            pending = await PutIncomingAsync(peerId, outgoingMessage, from, to, logicalId,
                ttlPeriod);
        }

        await _unitOfWork.SaveAsync();
        if (pending != null)
        {
            await DeliverAsync(pending.Value);
        }
        await IndexOutgoingAsync(userId, outgoingMessage, from, to);
        if (pending != null)
        {
            await IndexIncomingAsync(pending.Value);
        }
        return new ShortSentBatch(userId, peerType, peerId, randomId,
            senderMessageId, pts, outgoingMessage.AsMessage().Date,
            outgoingMessage.AsSpan().ToArray());
    }

    // `deriveMentions` re-derives server-side mentions from the template's own
    // text before the copies are written. A forward must NOT do this: its
    // entities are the source row's, copied verbatim. A scheduled flush must, or
    // a queued message would silently lose the mention an immediate send creates.
    public async Task<ShortSentBatch> SendPreparedBasicGroupMessageAsync(long authKeyId,
        long userId, long chatId, IReadOnlyCollection<long> participantIds,
        byte[] templateBytes, long randomId, byte[]? chatBytes = null,
        bool deriveMentions = false)
    {
        var senderContext = _updatesContextFactory.GetUpdatesContext(authKeyId, userId);
        int senderMessageId = (int)await senderContext.NextMessageId();
        MentionPlan mentions = deriveMentions
            ? ResolveTemplateMentions(templateBytes)
            : MentionPlan.None;
        int ttlPeriod = await _expiry.ResolveTtlPeriodAsync(userId,
            TLPeer.PeerType.PeerChat, chatId);
        using TLPeer from = new PeerUser(userId);
        using TLPeer to = new PeerChat(chatId);
        using TLMessage outgoingMessage = WithLocalId(templateBytes, senderMessageId,
            mentions.EntitiesBytes, ttlPeriod);

        int senderPts = await PutOutgoingAsync(senderContext, userId,
            outgoingMessage, from, to);
        TrackExpiry(MessageExpiryBox.Common, userId, outgoingMessage, ttlPeriod);
        long logicalId = await _messages.CreateMessageCopyAsync(userId, senderMessageId);
        var pending = new List<PendingDelivery>();
        foreach (long participantId in participantIds)
        {
            if (participantId == userId)
            {
                continue;
            }
            pending.Add(await PutIncomingGroupAsync(participantId, chatId,
                outgoingMessage, from, logicalId,
                mentions.UserIds.Contains(participantId), ttlPeriod));
        }

        await _unitOfWork.SaveAsync();
        foreach (PendingDelivery delivery in pending)
        {
            await DeliverAsync(delivery);
        }
        await IndexOutgoingAsync(userId, outgoingMessage, from, to);
        foreach (PendingDelivery delivery in pending)
        {
            await IndexIncomingAsync(delivery);
        }
        return new ShortSentBatch(userId, TLPeer.PeerType.PeerChat, chatId, randomId,
            senderMessageId, senderPts, outgoingMessage.AsMessage().Date,
            outgoingMessage.AsSpan().ToArray(), chatBytes);
    }

    public async Task<ChannelSentBatch> SendPreparedChannelMessageAsync(long userId,
        long channelId, int forumTopicId, StoredMessageForumTopic? forumTopic,
        byte[] templateBytes, byte[] channelBytes, long randomId,
        bool deriveMentions = false)
    {
        var channelBox = new ChannelMessageBox(_counterFactory, channelId);
        int messageId = await channelBox.NextMessageId();
        MentionPlan mentions = deriveMentions
            ? ResolveTemplateMentions(templateBytes)
            : MentionPlan.None;
        int ttlPeriod = await _expiry.ResolveTtlPeriodAsync(userId,
            TLPeer.PeerType.PeerChannel, channelId);
        using TLMessage message = WithLocalId(templateBytes, messageId,
            mentions.EntitiesBytes, ttlPeriod);
        byte[] messageBytes = message.AsSpan().ToArray();
        int pts = await channelBox.IncrementPts();
        _channelMessagesRepository.PutMessage(channelId, message, pts);
        TrackExpiry(MessageExpiryBox.Channel, channelId, message, ttlPeriod);

        if (forumTopic != null)
        {
            using TLForumTopicInfo updatedTopic = ForumMessages.BuildStoredForumTopic(
                forumTopic with { TopMessage = messageId });
            _forumTopicsRepository.PutTopic(updatedTopic);
            await UpdateForumTopicUnreadStateAsync(channelId, forumTopicId, userId,
                messageId);
        }

        await _unitOfWork.SaveAsync();

        (TLPeer.PeerType AuthorType, long AuthorId) author =
            ReadMessageAuthor(message, userId);
        var memberIds = await _fanout.GetOtherActiveChannelMemberIdsAsync(channelId,
            userId);
        foreach (long memberId in memberIds)
        {
            await _fanout.EnqueueNewChannelMessageAsync(memberId, messageBytes, pts);
        }

        var searchModel = new MessageSearchModel(
            channelId + "_" + messageId, channelId,
            (int)author.AuthorType, author.AuthorId,
            (int)TLPeer.PeerType.PeerChannel, channelId, messageId,
            null, Encoding.UTF8.GetString(message.AsMessage().MessageProperty),
            message.AsMessage().Date);
        await _search.IndexMessage(searchModel);

        return new ChannelSentBatch(userId, channelId, randomId, messageId, pts,
            message.AsMessage().Date, messageBytes, channelBytes);
    }

    private static (TLPeer.PeerType Type, long Id) ReadMessageAuthor(
        TLMessage message, long fallbackUserId)
    {
        if (message.Type == TLMessage.MessageType.Message)
        {
            var body = message.AsMessage();
            if (body.Flags[8] && PeerResolver.TryReadPeer(body.Get_FromIdView(),
                    out var author))
            {
                return author;
            }
        }
        return (TLPeer.PeerType.PeerUser, fallbackUserId);
    }

    // The template carries every field of the row except its box-local id, which
    // only the owning box can allocate. Derived mention entities are merged in
    // here for the same reason: they are not known until the row is being sent.
    private static TLMessage WithLocalId(byte[] templateBytes, int messageId,
        byte[]? entitiesOverride = null, int ttlPeriod = 0)
    {
        using var template = new TLMessage(templateBytes, 0, templateBytes.Length);
        // A row copied out of another dialog must not keep that dialog's timer,
        // and clearing a value-gated flag is the one thing a builder cannot do.
        if (ttlPeriod <= 0 && template.AsMessage().Flags[25])
        {
            using TLMessage retimed = MessageRows.RebuildTtl(template.AsMessage(), 0);
            return AssembleLocalRow(retimed.AsMessage(), messageId, entitiesOverride,
                0);
        }
        return AssembleLocalRow(template.AsMessage(), messageId, entitiesOverride,
            ttlPeriod);
    }

    private static TLMessage AssembleLocalRow(Message source, int messageId,
        byte[]? entitiesOverride, int ttlPeriod)
    {
        var builder = source.Clone().Id(messageId);
        if (entitiesOverride != null)
        {
            builder = builder.Entities(new Vector(entitiesOverride.AsSpan()));
        }
        if (ttlPeriod > 0)
        {
            builder = builder.TtlPeriod(ttlPeriod);
        }
        return builder.Build();
    }

    private void TrackExpiry(int boxType, long boxId, TLMessage message,
        int ttlPeriod)
    {
        if (ttlPeriod <= 0)
        {
            return;
        }
        var stored = message.AsMessage();
        _expiry.Track(boxType, boxId, stored.Id, stored.Date, ttlPeriod);
    }

    public async Task<PipelineResult<ReactionCallerBatch>> SendChannelReactionAsync(
        long userId, long channelId, int msgId, List<byte[]> requested, bool big,
        bool broadcast, int uniqueLimit, int date, long order, byte[] callerPeerBytes)
    {
        var saved = await _channelMessagesRepository
            .GetMessageAsync(channelId, msgId);
        if (saved == null)
        {
            return PipelineResult<ReactionCallerBatch>.Failure("MESSAGE_ID_INVALID");
        }

        var rows = await _messageReactionsRepository
            .GetReactionsAsync(MessageReactionBox.Channel, channelId, msgId);
        var otherEntries = ReactionStore.ReadReactionEntries(rows, excludeUserId: userId);
        if (ReactionStore.ExceedsUniqueLimit(otherEntries, requested, uniqueLimit))
        {
            saved.Value.Dispose();
            return PipelineResult<ReactionCallerBatch>.Failure("REACTIONS_TOO_MANY");
        }

        long postAuthorId;
        List<ReactionEntry> merged;
        using (var savedMessage = saved.Value)
        {
            var savedBody = savedMessage.AsSavedMessage();
            int storedPts = savedBody.Pts;
            var original = savedBody.Get_OriginalMessage();
            if (original.Type != TLMessage.MessageType.Message)
            {
                return PipelineResult<ReactionCallerBatch>.Failure("MESSAGE_ID_INVALID");
            }
            postAuthorId = ReactionStore.ResolveChannelPostAuthorId(original);

            bool unread = requested.Count > 0 && postAuthorId > 0 && postAuthorId != userId;
            merged = ReactionStore.MergeCallerEntry(otherEntries, userId, requested, big,
                unread, date, order);
            byte[] neutralBytes = ReactionStore.BuildMessageReactionsValue(merged,
                viewerId: 0, includeRecent: !broadcast, canSeeList: !broadcast,
                includeUnread: false);
            using TLMessage updated = original.AsMessage().Clone()
                .Reactions(neutralBytes)
                .Build();
            _channelMessagesRepository.PutMessage(channelId, updated, storedPts);

            if (requested.Count == 0)
            {
                _messageReactionsRepository.DeleteReaction(
                    MessageReactionBox.Channel, channelId, msgId, userId);
            }
            else
            {
                using TLMessageReactionInfo row = ReactionStore.BuildReactionRow(
                    MessageReactionBox.Channel, channelId, msgId, userId,
                    (int)TLPeer.PeerType.PeerChannel, channelId, big, unread, date, order,
                    requested);
                _messageReactionsRepository.PutReaction(row);
            }
        }

        // Channel reactions commit before live fan-out; keep this asymmetry exact.
        await _unitOfWork.SaveAsync();
        var memberIds = await _fanout.GetOtherActiveChannelMemberIdsAsync(channelId, userId);
        foreach (long memberId in memberIds)
        {
            byte[] memberViewBytes = ReactionStore.BuildMessageReactionsValue(merged,
                memberId, includeRecent: !broadcast, canSeeList: !broadcast,
                includeUnread: memberId == postAuthorId);
            await _fanout.EnqueueMessageReactionsAsync(memberId, callerPeerBytes, msgId,
                memberViewBytes);
        }

        byte[] callerViewBytes = ReactionStore.BuildMessageReactionsValue(merged, userId,
            includeRecent: !broadcast, canSeeList: !broadcast, includeUnread: false);
        _log.Debug($"💟 SendReaction user:{userId} channel:{channelId} msg:{msgId} " +
                   $"reactions:{requested.Count} members:{memberIds.Count}");
        return PipelineResult<ReactionCallerBatch>.Success(new ReactionCallerBatch(
            userId, callerPeerBytes, msgId, callerViewBytes, merged, channelId));
    }

    public async Task<PipelineResult<ReactionCallerBatch>> SendCommonBoxReactionAsync(
        long userId, TLPeer.PeerType peerType, long peerId, int msgId,
        List<byte[]> requested, bool big, int uniqueLimit, int date, long order,
        byte[] callerPeerBytes)
    {
        var callerMessage = await _messageRepository.GetMessageAsync(userId, msgId);
        if (callerMessage == null)
        {
            return PipelineResult<ReactionCallerBatch>.Failure("MESSAGE_ID_INVALID");
        }
        callerMessage.Value.Dispose();

        var callerRows = await _messageReactionsRepository
            .GetReactionsAsync(MessageReactionBox.Common, userId, msgId);
        var otherEntries = ReactionStore.ReadReactionEntries(callerRows,
            excludeUserId: userId);
        if (ReactionStore.ExceedsUniqueLimit(otherEntries, requested, uniqueLimit))
        {
            return PipelineResult<ReactionCallerBatch>.Failure("REACTIONS_TOO_MANY");
        }

        var copies = new List<(long OwnerId, int MessageId)>();
        var reverse = await _messageReactionsRepository
            .GetCopyByOwnerMessageAsync(userId, msgId);
        if (reverse != null)
        {
            long logicalId;
            using (var reverseRow = reverse.Value)
            {
                logicalId = reverseRow.AsMessageCopyInfo().LogicalId;
            }
            var copyRows = await _messageReactionsRepository
                .GetMessageCopiesAsync(logicalId);
            foreach (var copyRow in copyRows)
            {
                using var copy = copyRow;
                var info = copy.AsMessageCopyInfo();
                copies.Add((info.UserId, info.MessageId));
            }
        }
        if (copies.Count == 0)
        {
            copies.Add((userId, msgId));
        }

        bool isGroup = peerType == TLPeer.PeerType.PeerChat;
        byte[]? callerViewBytes = null;
        List<ReactionEntry>? callerMerged = null;
        foreach (var (ownerId, ownerMsgId) in copies)
        {
            var saved = await _messageRepository
                .GetMessageAsync(ownerId, ownerMsgId);
            if (saved == null)
            {
                continue;
            }

            var rows = await _messageReactionsRepository
                .GetReactionsAsync(MessageReactionBox.Common, ownerId, ownerMsgId);
            var copyOtherEntries = ReactionStore.ReadReactionEntries(rows,
                excludeUserId: userId);

            byte[]? ownerUpdatePeerBytes = null;
            byte[]? ownerUpdateReactionBytes = null;
            using (var savedMessage = saved.Value)
            {
                var savedBody = savedMessage.AsSavedMessage();
                int storedPts = savedBody.Pts;
                var original = savedBody.Get_OriginalMessage();
                if (original.Type != TLMessage.MessageType.Message)
                {
                    continue;
                }

                var message = original.AsMessage();
                bool ownerAuthored = message.OutProperty;
                bool unread = requested.Count > 0 && ownerId != userId && ownerAuthored;
                byte[] ownerPeerBytes = message.PeerId.ToArray();
                var ownerPeerType = peerType;
                long ownerPeerId = peerId;
                if (PeerResolver.TryReadPeer(message.Get_PeerIdView(), out var copyPeer))
                {
                    ownerPeerType = copyPeer.Type;
                    ownerPeerId = copyPeer.Id;
                }
                var merged = ReactionStore.MergeCallerEntry(copyOtherEntries, userId,
                    requested, big, unread, date, order);
                byte[] viewBytes = ReactionStore.BuildMessageReactionsValue(merged, ownerId,
                    includeRecent: true, canSeeList: isGroup, includeUnread: true);
                using TLMessage updated = message.Clone()
                    .Reactions(viewBytes)
                    .Build();
                _messageRepository.PutMessage(ownerId, updated, storedPts);

                if (requested.Count == 0)
                {
                    _messageReactionsRepository.DeleteReaction(
                        MessageReactionBox.Common, ownerId, ownerMsgId, userId);
                }
                else
                {
                    using TLMessageReactionInfo row = ReactionStore.BuildReactionRow(
                        MessageReactionBox.Common, ownerId, ownerMsgId, userId,
                        (int)ownerPeerType, ownerPeerId, big, unread, date, order,
                        requested);
                    _messageReactionsRepository.PutReaction(row);
                }

                if (ownerId == userId)
                {
                    callerViewBytes = viewBytes;
                    callerMerged = merged;
                }
                else
                {
                    ownerUpdatePeerBytes = ownerPeerBytes;
                    ownerUpdateReactionBytes = viewBytes;
                }
            }

            // Common-box fan-out is intentionally interleaved with each copy mutation.
            if (ownerUpdatePeerBytes != null && ownerUpdateReactionBytes != null)
            {
                await _fanout.EnqueueMessageReactionsAsync(ownerId,
                    ownerUpdatePeerBytes, ownerMsgId, ownerUpdateReactionBytes);
            }
        }

        await _unitOfWork.SaveAsync();
        if (callerViewBytes == null || callerMerged == null)
        {
            return PipelineResult<ReactionCallerBatch>.Failure("MESSAGE_ID_INVALID");
        }

        _log.Debug($"💟 SendReaction user:{userId} peerType:{peerType} peer:{peerId} " +
                   $"msg:{msgId} reactions:{requested.Count} copies:{copies.Count}");
        return PipelineResult<ReactionCallerBatch>.Success(new ReactionCallerBatch(
            userId, callerPeerBytes, msgId, callerViewBytes, callerMerged,
            isGroup ? peerId : 0));
    }

    private async Task<int> PutOutgoingAsync(IUpdatesContext senderContext,
        long ownerId, TLMessage outgoingMessage, TLPeer from, TLPeer to)
    {
        var (previousPts, pts) = await _messages.PutOutgoingMessageAsync(
            senderContext, ownerId, outgoingMessage);
        _log.Debug($"💬 Message was sent Sender: {ownerId} Previous PTS: {previousPts} PTS: {pts}");
        return pts;
    }

    private async Task IndexOutgoingAsync(long ownerId, TLMessage outgoingMessage,
        TLPeer from, TLPeer to)
    {
        await _search.IndexMessage(new MessageSearchModel(
            GetPeerId(from) + "_" + outgoingMessage.AsMessage().Id,
            GetPeerId(from), (int)from.Type, GetPeerId(from),
            (int)to.Type, GetPeerId(to), outgoingMessage.AsMessage().Id,
            null, Encoding.UTF8.GetString(outgoingMessage.AsMessage().MessageProperty),
            outgoingMessage.AsMessage().Date));
    }

    private async Task<PendingDelivery> PutIncomingAsync(long recipientId,
        TLMessage outgoingMessage, TLPeer from, TLPeer to, long logicalId,
        int ttlPeriod = 0)
    {
        StoredMessageWrite stored = await _messages.PutIncomingMessageAsync(recipientId,
            outgoingMessage, from, logicalId);
        using var incomingMessage = new TLMessage(stored.Bytes, 0, stored.Bytes.Length);
        TrackExpiry(MessageExpiryBox.Common, recipientId, incomingMessage, ttlPeriod);
        var searchModel = new MessageSearchModel(
            recipientId + "_" + incomingMessage.AsMessage().Id,
            recipientId, (int)to.Type, recipientId,
            (int)from.Type, GetPeerId(from), incomingMessage.AsMessage().Id,
            null, Encoding.UTF8.GetString(incomingMessage.AsMessage().MessageProperty),
            incomingMessage.AsMessage().Date);
        return new PendingDelivery(recipientId, stored.Bytes, stored.Pts, searchModel);
    }

    private async Task<PendingDelivery> PutIncomingGroupAsync(long participantId,
        long chatId, TLMessage outgoingMessage, TLPeer from, long logicalId,
        bool mentioned = false, int ttlPeriod = 0)
    {
        StoredMessageWrite stored = await _messages.PutIncomingGroupMessageAsync(
            participantId, chatId, outgoingMessage, logicalId, mentioned);
        using var incomingMessage = new TLMessage(stored.Bytes, 0, stored.Bytes.Length);
        TrackExpiry(MessageExpiryBox.Common, participantId, incomingMessage,
            ttlPeriod);
        var message = incomingMessage.AsMessage();
        var searchModel = new MessageSearchModel(
            participantId + "_" + message.Id,
            participantId, (int)from.Type, GetPeerId(from),
            (int)TLPeer.PeerType.PeerChat, chatId, message.Id,
            null, Encoding.UTF8.GetString(message.MessageProperty), message.Date);
        return new PendingDelivery(participantId, stored.Bytes, stored.Pts,
            searchModel);
    }

    private async Task DeliverAsync(PendingDelivery delivery)
    {
        await _fanout.EnqueueNewMessageAsync(delivery.RecipientId,
            delivery.MessageBytes, delivery.Pts);
    }

    private async Task IndexIncomingAsync(PendingDelivery delivery)
    {
        await _search.IndexMessage(delivery.SearchModel);
    }

    private async Task UpdateForumTopicUnreadStateAsync(long channelId, int topicId,
        long senderId, int messageId)
    {
        var participants = await _chatParticipantsRepository
            .GetParticipantsAsync(channelId);
        foreach (var participant in participants)
        {
            using var row = participant;
            if (!IsActiveParticipant(row))
            {
                continue;
            }
            long userId = row.AsChatParticipantInfo().UserId;
            int inbox = 0;
            int outbox = 0;
            int unread = 0;
            int unreadMentions = 0;
            int unreadReactions = 0;
            using (var oldState = await _forumTopicsRepository.GetReadStateAsync(
                       channelId, topicId, userId))
            {
                if (oldState != null)
                {
                    var state = oldState.Value.AsForumTopicReadState();
                    inbox = state.ReadInboxMaxId;
                    outbox = state.ReadOutboxMaxId;
                    unread = state.UnreadCount;
                    unreadMentions = state.UnreadMentionsCount;
                    unreadReactions = state.UnreadReactionsCount;
                }
            }
            if (userId == senderId) outbox = Math.Max(outbox, messageId);
            else unread++;
            using TLForumTopicReadState updated = ForumTopicReadState.Builder()
                .ChannelId(channelId).TopicId(topicId).UserId(userId)
                .ReadInboxMaxId(inbox).ReadOutboxMaxId(outbox).UnreadCount(unread)
                .UnreadMentionsCount(unreadMentions)
                .UnreadReactionsCount(unreadReactions).Build();
            _forumTopicsRepository.PutReadState(updated);
        }
    }

    // Server-generated mention entities plus the users they name. Only group and
    // megagroup sends resolve one: a private message needs no mention to reach
    // its recipient, and the pinned client ignores the flag there
    // (`MessagesManager.cpp:11193`).
    private readonly record struct MentionPlan(byte[]? EntitiesBytes,
        IReadOnlySet<long> UserIds)
    {
        public static MentionPlan None { get; } =
            new(null, new HashSet<long>());
    }

    private MentionPlan ResolveMentions(byte[] requestBytes)
    {
        string text;
        byte[]? clientEntities;
        var userIds = new HashSet<long>();
        using (var request = new TLBytes(requestBytes, 0, requestBytes.Length))
        {
            var sendMessage = (SendMessage)request;
            text = Encoding.UTF8.GetString(sendMessage.Message);
            clientEntities = sendMessage.Flags[3]
                ? sendMessage.Entities.ToReadOnlySpan().ToArray()
                : null;
            if (clientEntities != null)
            {
                CollectNamedMentions(sendMessage.Entities, userIds);
            }
        }
        return ResolveMentions(text, clientEntities, userIds);
    }

    /// <summary>
    /// The same derivation against an already assembled row rather than a send
    /// request. A scheduled entry is stored as a `message`, so its flush resolves
    /// its mentions from the row it is about to send.
    /// </summary>
    private MentionPlan ResolveTemplateMentions(byte[] templateBytes)
    {
        string text;
        byte[]? existingEntities;
        var userIds = new HashSet<long>();
        using (var template = new TLMessage(templateBytes, 0, templateBytes.Length))
        {
            if (template.Type != TLMessage.MessageType.Message)
            {
                return MentionPlan.None;
            }
            var message = template.AsMessage();
            text = Encoding.UTF8.GetString(message.MessageProperty);
            existingEntities = message.Flags[7]
                ? message.Entities.ToReadOnlySpan().ToArray()
                : null;
            if (existingEntities != null)
            {
                CollectNamedMentions(message.Entities, userIds);
            }
        }
        return ResolveMentions(text, existingEntities, userIds);
    }

    private MentionPlan ResolveMentions(string text, byte[]? clientEntities,
        HashSet<long> userIds)
    {
        List<MessageMentions.UsernameToken> tokens =
            MessageMentions.ScanUsernames(text);
        var resolved = new List<MessageMentions.UsernameToken>(tokens.Count);
        foreach (MessageMentions.UsernameToken token in tokens)
        {
            using TLUser? user = _userRepository
                .GetUserByUsername(token.Username);
            if (user == null)
            {
                continue;
            }
            userIds.Add(user.Value.AsUser().Id);
            resolved.Add(token);
        }
        if (resolved.Count == 0)
        {
            return new MentionPlan(null, userIds);
        }

        var entities = clientEntities == null
            ? new Vector()
            : new Vector(clientEntities.AsSpan());
        MessageMentions.AppendMentionEntities(ref entities, resolved);
        return new MentionPlan(entities.ToReadOnlySpan().ToArray(), userIds);
    }

    private static void CollectNamedMentions(Vector entities, HashSet<long> userIds)
    {
        int count = entities.Count;
        for (int i = 0; i < count; i++)
        {
            var entity = (MessageEntityView)entities.ReadTLObject();
            if (entity.Is(out MessageEntityMentionName named))
            {
                userIds.Add(named.UserId);
            }
        }
    }

    /// <summary>
    /// Builds the row a scheduled-queue entry stores. It is an ordinary outgoing
    /// message whose id is the queue's scheduled id and whose date is the send
    /// date, which is exactly what `updateNewScheduledMessage` must report
    /// (/api/scheduled-messages). Mention entities are deliberately NOT derived
    /// here: they are resolved when the row is actually flushed, so a username
    /// that changes between scheduling and sending resolves as it does at send
    /// time.
    /// </summary>
    public static TLMessage BuildScheduledTemplate(in TLBytes sendMessage,
        int scheduledId, in TLPeer from, in TLPeer to, int sendDate,
        bool outgoing = true, bool post = false, bool forumTopic = false,
        byte[]? media = null, long groupedId = 0) =>
        GenerateOutgoingMessage(sendMessage, scheduledId, from, to, sendDate,
            outgoing, post, forumTopic, media, groupedId);

    private static TLMessage GenerateOutgoingMessage(in TLBytes sendMessage,
        int senderMessageId, in TLPeer from, in TLPeer to, int date,
        bool outgoing = true, bool post = false, bool forumTopic = false,
        byte[]? media = null, long groupedId = 0, byte[]? entitiesOverride = null,
        int ttlPeriod = 0)
    {
        var request = (SendMessage)sendMessage;
        var builder = Message.Builder()
            .Id(senderMessageId)
            .OutProperty(outgoing)
            .Post(post)
            .Silent(request.Silent)
            .Noforwards(request.Noforwards)
            .InvertMedia(request.InvertMedia)
            .FromId(from.AsSpan())
            .PeerId(to.AsSpan())
            .MessageProperty(request.Message)
            .Date(date);
        if (media != null)
        {
            builder = builder.Media(media);
        }
        if (groupedId != 0)
        {
            builder = builder.GroupedId(groupedId);
        }
        if (post)
        {
            // Broadcast posts must advertise a positive initial view count or
            // pinned TDLib will never schedule messages.getMessagesViews.
            builder = builder.Views(1);
        }
        if (ttlPeriod > 0)
        {
            builder = builder.TtlPeriod(ttlPeriod);
        }

        var flags = request.Flags;
        if (flags[0] && request.Get_ReplyToView().Is(out InputReplyToMessage replyTo))
        {
            using TLMessageReplyHeader replyToHeader = MessageReplyHeaders
                .FromInputReplyToMessage(replyTo, forumTopic);
            builder = builder.ReplyTo(replyToHeader.AsSpan());
        }
        if (flags[2]) builder = builder.ReplyMarkup(request.ReplyMarkup);
        if (entitiesOverride != null)
        {
            builder = builder.Entities(new Vector(entitiesOverride.AsSpan()));
        }
        else if (flags[3])
        {
            builder = builder.Entities(request.Entities);
        }
        return builder.Build();
    }

    private static bool IsActiveParticipant(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }

    private static int UnixNow() =>
        (int)DateTimeOffset.Now.ToUnixTimeSeconds();

    private static long GetPeerId(TLPeer peer) => peer.Type switch
    {
        TLPeer.PeerType.PeerUser => peer.AsPeerUser().UserId,
        TLPeer.PeerType.PeerChat => peer.AsPeerChat().ChatId,
        TLPeer.PeerType.PeerChannel => peer.AsPeerChannel().ChannelId,
        _ => 0
    };
}
