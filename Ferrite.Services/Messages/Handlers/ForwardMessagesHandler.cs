// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Channels;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.MessageMethods;

/// <summary>
/// Copies existing messages into another dialog. The copied text/media go through
/// the ordinary send target, persistence, pts, and fan-out machinery; what makes a
/// forward a forward is the `messageFwdHeader` that credits the original author.
/// Source ids are read in the box that owns them: the caller's own common box for
/// a user or basic group, the shared channel box for a channel.
/// </summary>
public sealed class ForwardMessagesHandler
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IForumTopicsRepository _forumTopicsRepository;
    private readonly IMessagingSettingsRepository _messagingSettingsRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IStatisticsRepository _statisticsRepository;

    private const int MaxForwardedMessages = 100;

    private readonly IUnitOfWork _unitOfWork;
    private readonly MessageLocator _locator;
    private readonly SendPipeline _send;
    private readonly UpdateFanout _fanout;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly IdAllocators _ids;
    private readonly ScheduledMessageStore _scheduled;
    private readonly IUpdatesService _updates;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _log;

    public ForwardMessagesHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IStatisticsRepository statisticsRepository, MessageLocator locator,
        SendPipeline send, UpdateFanout fanout,
        IUpdatesContextFactory updatesContextFactory, IdAllocators ids,
        ScheduledMessageStore scheduled, IUpdatesService updates,
        TimeProvider timeProvider, ILogger log)
    {
        _chatParticipantsRepository = chatParticipantsRepository;
        _forumTopicsRepository = forumTopicsRepository;
        _messagingSettingsRepository = messagingSettingsRepository;

        _authorizationRepository = authorizationRepository;
        _chatRepository = chatRepository;
        _statisticsRepository = statisticsRepository;

        _unitOfWork = unitOfWork;
        _locator = locator;
        _send = send;
        _fanout = fanout;
        _updatesContextFactory = updatesContextFactory;
        _ids = ids;
        _scheduled = scheduled;
        _updates = updates;
        _timeProvider = timeProvider;
        _log = log;
    }

    [TLFunction(Constructors.baseLayer_ForwardMessages)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return Error(400, "AUTH_KEY_INVALID");
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        var view = (ForwardMessages)q;
        if (!PeerResolver.TryResolveInputPeerDialogKey(view.Get_FromPeerView(),
                userId, out DialogPeerKey fromPeer) ||
            !PeerResolver.TryResolveInputPeerDialogKey(view.Get_ToPeerView(),
                userId, out DialogPeerKey toPeer))
        {
            return Error(400, "PEER_ID_INVALID");
        }
        ForwardRequest request = ReadRequest(view, fromPeer, toPeer, userId);

        if (request.QuickReply || request.SuggestedPost)
        {
            // Business quick replies and paid suggested posts are products Ferrite
            // does not run; 403 keeps the refusal out of the client's retry path.
            return Error(403, "METHOD_DISABLED");
        }
        if (request.Ids.Length == 0)
        {
            return Error(400, "MESSAGE_IDS_EMPTY");
        }
        if (request.Ids.Length != request.RandomIds.Length)
        {
            return Error(400, "RANDOM_ID_INVALID");
        }
        if (request.Ids.Length > MaxForwardedMessages)
        {
            return Error(400, "MESSAGE_ID_INVALID");
        }

        (List<ForwardSource> sources, string? sourceError, int sourceCode) =
            await LoadSourcesAsync(userId, request);
        if (sourceError != null)
        {
            return Error(sourceCode, sourceError);
        }

        int forumTopicId = request.ForumTopicId;
        PreparedMessageTarget target = await MessageSendTargetResolver.PrepareAsync(_chatRepository, _chatParticipantsRepository, _forumTopicsRepository, _messagingSettingsRepository, userId, request.ToPeer.Type, request.ToPeer.Id,
            Array.Empty<byte>(), new[] { ChatBannedAction.SendMessages },
            explicitForumTopicId: forumTopicId,
            explicitSender: request.SendAs,
            hasExplicitSender: request.HasSendAs);
        if (target.Error != null)
        {
            // The shared send-target resolver's errors are reported with the same
            // code the ordinary send handlers use for them.
            return Error(400, target.Error);
        }

        bool destinationIsSelf = target.PeerType == TLPeer.PeerType.PeerUser &&
                                 target.PeerId == userId;
        // Resolved here rather than inside the send loop: the destination row is a
        // ref-struct view and every send in that loop is an await.
        bool destinationIsPublicChannel = IsPublicChannel(target);
        int date = UnixNow();
        bool queued = request.ScheduleDate > 0 &&
                      ScheduledMessageStore.IsQueued(request.ScheduleDate, date);
        var groupedIds = new Dictionary<long, long>();
        var scheduled = new List<ScheduledMessageStore.ScheduledSnapshot>(
            sources.Count);
        var sent = new List<ForwardedMessage>(sources.Count);
        for (int i = 0; i < sources.Count; i++)
        {
            ForwardSource source = sources[i];
            long groupedId = 0;
            if (source.GroupedId != 0)
            {
                // An album stays one album in the destination, but under a fresh
                // grouping key so re-forwarding it does not merge with the first
                // copy already in that dialog.
                if (!groupedIds.TryGetValue(source.GroupedId, out groupedId))
                {
                    groupedId = await _ids.NextMediaGroupIdAsync();
                    groupedIds[source.GroupedId] = groupedId;
                }
            }

            long randomId = request.RandomIds[i];
            if (queued)
            {
                // A scheduled forward stores exactly the row an immediate forward
                // would have written, so the flush copies the same provenance,
                // media and grouping the client asked for.
                byte[] queuedTemplate = BuildTemplate(source, request, target,
                    request.ScheduleDate, groupedId, destinationIsSelf);
                ScheduledMessageStore.ScheduledSnapshot? entry = await _scheduled
                    .EnqueueAsync(userId, target.PeerType, target.PeerId, randomId,
                        request.ScheduleDate, queuedTemplate);
                if (entry == null)
                {
                    return Error(400, "SCHEDULE_TOO_MUCH");
                }
                scheduled.Add(entry.Value);
                continue;
            }

            byte[] template = BuildTemplate(source, request, target, date,
                groupedId, destinationIsSelf);
            if (target.PeerType == TLPeer.PeerType.PeerChannel)
            {
                ChannelSentBatch batch = await _send.SendPreparedChannelMessageAsync(
                    userId, target.PeerId, target.ForumTopicId, target.ForumTopic,
                    template, target.ChatBytes!, randomId);
                sent.Add(new ForwardedMessage(batch.Id, randomId, batch.Pts,
                    batch.MessageBytes, true));
                continue;
            }

            ShortSentBatch common = target.PeerType == TLPeer.PeerType.PeerChat
                ? await _send.SendPreparedBasicGroupMessageAsync(authKeyId, userId,
                    target.PeerId, target.RelatedUserIds, template, randomId,
                    target.ChatBytes)
                : await _send.SendPreparedPrivateMessageAsync(authKeyId, userId,
                    target.PeerType, target.PeerId, template, randomId);
            sent.Add(new ForwardedMessage(common.Id, randomId, common.Pts,
                common.MessageBytes, false));
        }

        if (queued)
        {
            _log.Debug($"↪️ ForwardMessages user:{userId} scheduled:{scheduled.Count} " +
                       $"to:{target.PeerType}:{target.PeerId} at:{request.ScheduleDate}");
            return await BuildScheduledResultAsync(authKeyId, userId, request, target,
                scheduled, date);
        }

        await RecordPublicForwardsAsync(request, target, destinationIsPublicChannel,
            sources, sent, date);

        _log.Debug($"↪️ ForwardMessages user:{userId} " +
                   $"from:{request.FromPeer.Type}:{request.FromPeer.Id} " +
                   $"to:{target.PeerType}:{target.PeerId} count:{sent.Count}");
        return await BuildResultAsync(authKeyId, userId, request, target, sent, date);
    }

    /// <summary>
    /// Whether the destination is a channel a stranger could also have found,
    /// which is what makes a forward into it PUBLIC. An ACTIVE username is the
    /// test rather than the editable one, because a deactivated username no
    /// longer resolves.
    /// </summary>
    private static bool IsPublicChannel(PreparedMessageTarget target)
    {
        if (target.PeerType != TLPeer.PeerType.PeerChannel ||
            target.ChatBytes is not { Length: > 0 } chatBytes)
        {
            return false;
        }

        using var chat = new TLChat(chatBytes, 0, chatBytes.Length);
        return chat.Type == TLChat.ChatType.Channel &&
               ChannelUsernames.HasActive(
                   ChannelUsernames.Read(chat.AsChannel()));
    }

    /// <summary>
    /// Indexes the forwards `stats.getMessagePublicForwards` reads back, keyed by
    /// the SOURCE post. Only a channel post forwarded into a PUBLIC channel is
    /// indexed: a forward into a private destination is not something a stranger
    /// could have observed, and reporting it would leak the destination to the
    /// source channel's admins.
    ///
    /// The index is written here rather than in the send pipeline because this is
    /// the only place that knows both ends of the forward, and it FLUSHES ITSELF:
    /// the send pipeline's own flush has already happened by now, and a write
    /// left pending when the request's storage scope ends is an error rather
    /// than a lost row.
    /// </summary>
    private async Task RecordPublicForwardsAsync(ForwardRequest request,
        PreparedMessageTarget target, bool destinationIsPublicChannel,
        List<ForwardSource> sources, List<ForwardedMessage> sent, int date)
    {
        if (!destinationIsPublicChannel ||
            request.FromPeer.Type != TLPeer.PeerType.PeerChannel ||
            sent.Count == 0)
        {
            return;
        }

        // Every non-queued iteration appends exactly one sent row, so the two
        // lists are index-paired; a queued forward returns before reaching here.
        for (int i = 0; i < sent.Count && i < sources.Count; i++)
        {
            using TLPublicForwardRef row = PublicForwardRef.Builder()
                .ChannelId(request.FromPeer.Id)
                .MsgId(sources[i].MessageId)
                .FwdChannelId(target.PeerId)
                .FwdMsgId(sent[i].Id)
                .Date(date)
                .Build();
            _statisticsRepository.PutPublicForward(row);
        }

        await _unitOfWork.SaveAsync();
    }

    /// <summary>
    /// Reads every source row and rejects the whole request if one id cannot be
    /// forwarded. A partial forward would leave the client's random ids unpaired
    /// with no way to tell which copy is missing.
    /// </summary>
    private async Task<(List<ForwardSource> Sources, string? Error, int Code)>
        LoadSourcesAsync(long userId, ForwardRequest request)
    {
        var empty = new List<ForwardSource>();
        if (request.FromPeer.Id <= 0)
        {
            return (empty, "PEER_ID_INVALID", 400);
        }

        if (request.FromPeer.Type == TLPeer.PeerType.PeerChannel)
        {
            using (TLChat? channel = await _chatRepository
                       .GetChatAsync(request.FromPeer.Id))
            {
                if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
                {
                    return (empty, "CHANNEL_INVALID", 400);
                }
                if (channel.Value.AsChannel().Noforwards)
                {
                    return (empty, "CHAT_FORWARDS_RESTRICTED", 403);
                }
            }
            using (TLChatParticipantInfo? participant = await _chatParticipantsRepository.GetParticipantAsync(
                           request.FromPeer.Id, userId))
            {
                if (participant == null ||
                    !MessageEditRules.IsActiveParticipant(participant.Value))
                {
                    return (empty, "CHANNEL_PRIVATE", 400);
                }
            }
        }
        else if (request.FromPeer.Type == TLPeer.PeerType.PeerChat)
        {
            using TLChat? chat = await _chatRepository
                .GetChatAsync(request.FromPeer.Id);
            if (chat == null || chat.Value.Type != TLChat.ChatType.Chat ||
                chat.Value.AsChat().Deactivated)
            {
                return (empty, "CHAT_ID_INVALID", 400);
            }
            if (chat.Value.AsChat().Noforwards)
            {
                return (empty, "CHAT_FORWARDS_RESTRICTED", 403);
            }
        }

        var sources = new List<ForwardSource>(request.Ids.Length);
        foreach (int messageId in request.Ids)
        {
            if (messageId <= 0)
            {
                return (empty, "MESSAGE_ID_INVALID", 400);
            }

            StoredMessageLocation? location =
                request.FromPeer.Type == TLPeer.PeerType.PeerChannel
                    ? await _locator.FindChannelAsync(request.FromPeer.Id, messageId)
                    : await _locator.FindCommonAsync(userId, messageId);
            if (location == null)
            {
                return (empty, "MESSAGE_ID_INVALID", 400);
            }

            (ForwardSource? source, string? error) = ReadSource(location.Value,
                messageId, request.FromPeer);
            if (error != null)
            {
                return (empty, error, error == "CHAT_FORWARDS_RESTRICTED" ? 403 : 400);
            }
            sources.Add(source!.Value);
        }
        return (sources, null, 0);
    }

    private static (ForwardSource? Source, string? Error) ReadSource(
        StoredMessageLocation location, int messageId, DialogPeerKey fromPeer)
    {
        byte[] bytes = location.MessageBytes;
        using var stored = new TLMessage(bytes, 0, bytes.Length);
        if (stored.Type != TLMessage.MessageType.Message ||
            !MessageStore.TryReadStoredMessageInfo(stored, out StoredMessageInfo info) ||
            info.PeerType != fromPeer.Type || info.PeerId != fromPeer.Id)
        {
            // Service messages have no content to copy, and an id from another
            // conversation must not be reachable by naming an unrelated peer.
            return (null, "MESSAGE_ID_INVALID");
        }

        var message = stored.AsMessage();
        if (message.Noforwards)
        {
            return (null, "CHAT_FORWARDS_RESTRICTED");
        }
        return (new ForwardSource(messageId, bytes, message.GroupedId), null);
    }

    private static byte[] BuildTemplate(ForwardSource source, ForwardRequest request,
        PreparedMessageTarget target, int date, long groupedId,
        bool destinationIsSelf)
    {
        byte[] sourceBytes = source.MessageBytes;
        using var stored = new TLMessage(sourceBytes, 0, sourceBytes.Length);
        var message = stored.AsMessage();

        bool channel = target.PeerType == TLPeer.PeerType.PeerChannel;
        bool hasMedia = message.Flags[9];
        bool dropCaption = request.DropMediaCaptions && hasMedia;
        byte[]? fwdHeader = request.DropAuthor
            ? null
            : BuildForwardHeader(message, request.FromPeer, source.MessageId,
                destinationIsSelf);
        byte[]? replyHeader = BuildReplyHeader(request, target);

        using TLPeer from = PeerResolver.BuildPeer(target.Sender.Type,
            target.Sender.Id);
        using TLPeer to = PeerResolver.BuildPeer(target.PeerType, target.PeerId);
        var builder = Message.Builder()
            .Id(0)
            .OutProperty(!channel)
            .Post(channel && target.Broadcast)
            .Silent(request.Silent)
            .Noforwards(request.Noforwards)
            .InvertMedia(message.InvertMedia)
            .FromId(from.AsSpan())
            .PeerId(to.AsSpan())
            .Date(date)
            .MessageProperty(dropCaption
                ? ReadOnlySpan<byte>.Empty
                : message.MessageProperty);
        if (hasMedia)
        {
            builder = builder.Media(message.Media);
        }
        if (message.Flags[7] && !dropCaption)
        {
            builder = builder.Entities(message.Entities);
        }
        if (message.Flags[11])
        {
            builder = builder.ViaBotId(message.ViaBotId);
        }
        if (fwdHeader != null)
        {
            builder = builder.FwdFrom(fwdHeader);
        }
        if (replyHeader != null)
        {
            builder = builder.ReplyTo(replyHeader);
        }
        if (groupedId != 0)
        {
            builder = builder.GroupedId(groupedId);
        }

        using TLMessage template = builder.Build();
        return template.AsSpan().ToArray();
    }

    /// <summary>
    /// A re-forward keeps the provenance of the FIRST author, which is what the
    /// source row already carries. A fresh header credits a channel post to its
    /// channel and anything else to its sending user.
    /// </summary>
    private static byte[] BuildForwardHeader(Message message, DialogPeerKey fromPeer,
        int messageId, bool destinationIsSelf)
    {
        if (message.Flags[2] && !destinationIsSelf)
        {
            return message.FwdFrom.ToArray();
        }

        bool channelPost = fromPeer.Type == TLPeer.PeerType.PeerChannel && message.Post;
        using TLPeer originChannel = channelPost
            ? new PeerChannel(fromPeer.Id)
            : new TLPeer();
        using TLPeer savedFromPeer = destinationIsSelf
            ? PeerResolver.BuildPeer(fromPeer.Type, fromPeer.Id)
            : new TLPeer();

        var builder = MessageFwdHeader.Builder().Date(message.Date);
        if (message.Flags[2])
        {
            // Preserve the original credit, then add the jump-back pointer that
            // only a Saved Messages copy carries.
            var original = (MessageFwdHeaderView)message.FwdFrom;
            if (original.Is(out MessageFwdHeader existing))
            {
                builder = builder.Date(existing.Date);
                if (existing.Flags[0]) builder = builder.FromId(existing.FromId);
                if (existing.Flags[5]) builder = builder.FromName(existing.FromName);
                if (existing.Flags[2]) builder = builder.ChannelPost(existing.ChannelPost);
                if (existing.Flags[3]) builder = builder.PostAuthor(existing.PostAuthor);
                if (existing.Flags[6]) builder = builder.PsaType(existing.PsaType);
            }
        }
        else if (channelPost)
        {
            builder = builder.FromId(originChannel.AsSpan()).ChannelPost(messageId);
            if (message.Flags[16])
            {
                builder = builder.PostAuthor(message.PostAuthor);
            }
        }
        else if (message.Flags[8])
        {
            builder = builder.FromId(message.FromId);
        }
        if (destinationIsSelf)
        {
            builder = builder
                .SavedFromPeer(savedFromPeer.AsSpan())
                .SavedFromMsgId(messageId);
        }

        using TLMessageFwdHeader header = builder.Build();
        return header.AsSpan().ToArray();
    }

    // A forward carries a reply header only to name where the copy lands: an
    // explicit reply_to, or the forum topic the destination request selected.
    private static byte[]? BuildReplyHeader(ForwardRequest request,
        PreparedMessageTarget target)
    {
        bool forum = target.ForumTopic != null;
        if (request.ReplyToMessageId > 0)
        {
            using TLMessageReplyHeader header = MessageReplyHeader.Builder()
                .ReplyToMsgId(request.ReplyToMessageId)
                .ForumTopic(forum)
                .Build();
            return header.AsSpan().ToArray();
        }
        if (!forum || target.ForumTopicId <= 1)
        {
            return null;
        }

        using TLMessageReplyHeader topicHeader = MessageReplyHeaders.ForForumTopic(
            target.ForumTopicId);
        return topicHeader.AsSpan().ToArray();
    }

    private async Task<TLUpdates> BuildResultAsync(long authKeyId, long userId,
        ForwardRequest request, PreparedMessageTarget target,
        IReadOnlyList<ForwardedMessage> sent, int date)
    {
        var updateBytes = new List<byte[]>(sent.Count * 2);
        var userIds = new HashSet<long> { userId };
        var chatIds = new HashSet<long>();
        foreach (ForwardedMessage message in sent)
        {
            using (TLUpdate messageId = UpdateMessageID.Builder()
                       .Id(message.Id)
                       .RandomId(message.RandomId)
                       .Build())
            {
                updateBytes.Add(messageId.AsSpan().ToArray());
            }
            using (TLUpdate newMessage = message.Channel
                       ? UpdateNewChannelMessage.Builder()
                           .Message(message.MessageBytes)
                           .Pts(message.Pts)
                           .PtsCount(1)
                           .Build()
                       : UpdateNewMessage.Builder()
                           .Message(message.MessageBytes)
                           .Pts(message.Pts)
                           .PtsCount(1)
                           .Build())
            {
                updateBytes.Add(newMessage.AsSpan().ToArray());
            }

            // The client needs the ORIGINAL author's row to render "forwarded
            // from", not just the destination's participants.
            byte[] bytes = message.MessageBytes;
            using var stored = new TLMessage(bytes, 0, bytes.Length);
            MessageStore.AddMessageRelatedPeers(stored, userIds, chatIds);
            AddForwardOriginPeers(stored, userIds, chatIds);
        }

        if (target.PeerType == TLPeer.PeerType.PeerUser)
        {
            userIds.Add(target.PeerId);
        }
        else
        {
            chatIds.Add(target.PeerId);
        }
        if (request.FromPeer.Type == TLPeer.PeerType.PeerUser)
        {
            userIds.Add(request.FromPeer.Id);
        }
        else
        {
            chatIds.Add(request.FromPeer.Id);
        }

        List<byte[]> chats = await _fanout.GetChatBytesForViewerAsync(userId, chatIds);
        int seq = await _updatesContextFactory.GetUpdatesContext(authKeyId, userId)
            .IncrementSeq();
        return _fanout.BuildUpdates(updateBytes, userIds, chats, date, seq);
    }

    /// <summary>
    /// The answer to a forward that went into the schedule queue: the same
    /// `updateMessageID` plus `updateNewScheduledMessage` pair an ordinary scheduled
    /// send produces, so the client pairs each random id with its new queue entry.
    /// </summary>
    private async Task<TLUpdates> BuildScheduledResultAsync(long authKeyId,
        long userId, ForwardRequest request, PreparedMessageTarget target,
        IReadOnlyList<ScheduledMessageStore.ScheduledSnapshot> scheduled, int date)
    {
        if (!await _unitOfWork.SaveAsync())
        {
            return Error(500, "INTERNAL_SERVER_ERROR");
        }

        var updateBytes = new List<byte[]>(scheduled.Count * 2);
        var userIds = new HashSet<long> { userId };
        var chatIds = new HashSet<long>();
        foreach (ScheduledMessageStore.ScheduledSnapshot entry in scheduled)
        {
            using (TLUpdate messageId = UpdateMessageID.Builder()
                       .Id(entry.ScheduledId)
                       .RandomId(entry.RandomId)
                       .Build())
            {
                updateBytes.Add(messageId.AsSpan().ToArray());
            }
            using (TLUpdate created =
                   ScheduledMessageStore.BuildNewScheduledUpdate(entry))
            {
                updateBytes.Add(created.AsSpan().ToArray());
            }

            byte[] bytes = entry.MessageBytes;
            using var stored = new TLMessage(bytes, 0, bytes.Length);
            MessageStore.AddMessageRelatedPeers(stored, userIds, chatIds);
            AddForwardOriginPeers(stored, userIds, chatIds);

            await _updates.EnqueueUpdate(userId,
                ScheduledMessageStore.BuildNewScheduledUpdate(entry),
                UpdateDeliveryScope.ExcludingAuthKeys(new[] { authKeyId }));
        }

        if (target.PeerType == TLPeer.PeerType.PeerUser) userIds.Add(target.PeerId);
        else chatIds.Add(target.PeerId);
        if (request.FromPeer.Type == TLPeer.PeerType.PeerUser)
        {
            userIds.Add(request.FromPeer.Id);
        }
        else
        {
            chatIds.Add(request.FromPeer.Id);
        }

        List<byte[]> chats = await _fanout.GetChatBytesForViewerAsync(userId, chatIds);
        int seq = await _updatesContextFactory.GetUpdatesContext(authKeyId, userId)
            .IncrementSeq();
        return _fanout.BuildUpdates(updateBytes, userIds, chats, date, seq);
    }

    private static void AddForwardOriginPeers(TLMessage message,
        HashSet<long> userIds, HashSet<long> chatIds)
    {
        if (message.Type != TLMessage.MessageType.Message)
        {
            return;
        }
        var body = message.AsMessage();
        if (!body.Flags[2])
        {
            return;
        }

        var view = (MessageFwdHeaderView)body.FwdFrom;
        if (!view.Is(out MessageFwdHeader header) || !header.Flags[0] ||
            !PeerResolver.TryReadPeer(header.Get_FromIdView(), out var origin))
        {
            return;
        }
        if (origin.Type == TLPeer.PeerType.PeerUser)
        {
            userIds.Add(origin.Id);
        }
        else
        {
            chatIds.Add(origin.Id);
        }
    }

    // Await-safe snapshot of the request; every ref-struct view is read here.
    private sealed record ForwardRequest(DialogPeerKey FromPeer, DialogPeerKey ToPeer,
        int[] Ids, long[] RandomIds, bool Silent, bool DropAuthor,
        bool DropMediaCaptions, bool Noforwards, int ForumTopicId,
        int ReplyToMessageId, int ScheduleDate, bool QuickReply,
        bool SuggestedPost, bool HasSendAs, DialogPeerKey? SendAs);

    private readonly record struct ForwardSource(int MessageId, byte[] MessageBytes,
        long GroupedId);

    private readonly record struct ForwardedMessage(int Id, long RandomId, int Pts,
        byte[] MessageBytes, bool Channel);

    private static ForwardRequest ReadRequest(ForwardMessages view,
        DialogPeerKey fromPeer, DialogPeerKey toPeer, long userId)
    {
        VectorOfInt ids = view.Id;
        var messageIds = new int[ids.Count];
        for (int i = 0; i < ids.Count; i++)
        {
            messageIds[i] = ids[i];
        }
        VectorOfLong randoms = view.RandomId;
        var randomIds = new long[randoms.Count];
        for (int i = 0; i < randoms.Count; i++)
        {
            randomIds[i] = randoms[i];
        }

        int replyToMessageId = 0;
        int replyTopId = 0;
        if (view.Flags[22] &&
            view.Get_ReplyToView().Is(out InputReplyToMessage replyTo))
        {
            replyToMessageId = replyTo.ReplyToMsgId;
            replyTopId = replyTo.Flags[0] ? replyTo.TopMsgId : 0;
        }

        // The destination topic is named by top_msg_id, or by the reply's own
        // thread; topic 1 is the General topic every forum has.
        int forumTopicId = view.Flags[9] && view.TopMsgId > 0
            ? view.TopMsgId
            : replyTopId > 0
                ? replyTopId
                : replyToMessageId > 0
                    ? replyToMessageId
                    : 1;

        bool hasSendAs = view.Flags[13];
        DialogPeerKey? sendAs = hasSendAs
            ? PeerResolver.ResolveOptionalDialogPeer(view.Get_SendAsView(), userId)
            : null;

        return new ForwardRequest(fromPeer, toPeer, messageIds, randomIds,
            view.Silent, view.DropAuthor, view.DropMediaCaptions, view.Noforwards,
            forumTopicId, replyToMessageId, view.Flags[10] ? view.ScheduleDate : 0,
            view.Flags[17], view.Flags[23], hasSendAs, sendAs);
    }

    private int UnixNow() =>
        checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());

    private static TLUpdates Error(int code, string message) =>
        (TLUpdates)RpcErrorGenerator.GenerateError(code,
            Encoding.UTF8.GetBytes(message));
}
