// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.MessageMethods;

/// <summary>
/// Edits one already sent message. Only the fields whose request flags are
/// present change; every other field of the stored row survives, including the
/// ones a generated builder cannot re-set. Common-box messages are edited in all
/// of their per-owner copies at once, each keeping its own local id, in/out
/// perspective and pts; a channel post is a single shared row.
/// </summary>
public sealed class EditMessageHandler
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    private readonly IDocumentsRepository _documentsRepository;
    private readonly IPhotoRepository _photoRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IUpdatesService _updates;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly MessageLocator _locator;
    private readonly UpdateFanout _fanout;
    private readonly ICounterFactory _counterFactory;
    private readonly IUploadService _upload;
    private readonly IPhotoProcessingService _photos;
    private readonly PollStore _polls;
    private readonly ScheduledMessageStore _scheduled;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _log;

    public EditMessageHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IDocumentsRepository documentsRepository, IPhotoRepository photoRepository, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IUserRepository userRepository, IUpdatesService updates,
        IUpdatesContextFactory updatesContextFactory, MessageLocator locator,
        UpdateFanout fanout, ICounterFactory counterFactory, IUploadService upload,
        IPhotoProcessingService photos, PollStore polls,
        ScheduledMessageStore scheduled, TimeProvider timeProvider,
        ILogger log)
    {
        _chatParticipantsRepository = chatParticipantsRepository;

        _documentsRepository = documentsRepository;
        _photoRepository = photoRepository;

        _authorizationRepository = authorizationRepository;
        _chatRepository = chatRepository;
        _userRepository = userRepository;

        _scheduled = scheduled;
        _unitOfWork = unitOfWork;
        _updates = updates;
        _updatesContextFactory = updatesContextFactory;
        _locator = locator;
        _fanout = fanout;
        _counterFactory = counterFactory;
        _upload = upload;
        _photos = photos;
        _polls = polls;
        _timeProvider = timeProvider;
        _log = log;
    }

    [TLFunction(Constructors.baseLayer_EditMessage)]
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

        var request = (EditMessage)q;
        if (!PeerResolver.TryResolveInputPeerDialogKey(request.Get_PeerView(),
                userId, out DialogPeerKey peer))
        {
            return Error(400, "PEER_ID_INVALID");
        }
        RequestedEdit edit = ReadRequestedEdit(request);

        if (edit.QuickReply)
        {
            // Quick replies are a business product Ferrite does not run. Refusing
            // at the feature boundary with 403 keeps the branch visible and stays
            // out of the pinned client's 500-retry path.
            return Error(403, "METHOD_DISABLED");
        }
        if (edit.MessageId <= 0)
        {
            return Error(400, "MESSAGE_ID_INVALID");
        }
        if (edit.Scheduled)
        {
            // A `schedule_date` edit addresses the SCHEDULE QUEUE, not the message
            // box: the id names a queue entry. Pinned TDLib reaches this from
            // `editMessageSchedulingState` with a new date and no content
            // (`MessagesManager.cpp:23150-23153`), so it must be handled before the
            // not-modified check that an empty content edit would otherwise fail.
            return await EditScheduledMessageAsync(authKeyId, userId, peer, edit);
        }
        if (!edit.ReplacesText && !edit.ReplacesMedia && !edit.ReplacesReplyMarkup)
        {
            return Error(400, "MESSAGE_NOT_MODIFIED");
        }

        byte[]? mediaBytes = null;
        PollStore.PollInput? pollEdit = null;
        if (edit.ReplacesMedia)
        {
            if (PollStore.TryReadInputPoll(edit.InputMedia!, out var pollInput))
            {
                // The only poll edit the protocol defines is closing the poll the
                // message already carries; pinned TDLib reaches this path solely
                // through StopPollQuery (`PollManager.cpp:194-215`). Replacing a
                // live poll's question or options is not a supported operation,
                // so it is refused rather than silently rewritten.
                if (!pollInput.Closed)
                {
                    return Error(400, "MEDIA_INVALID");
                }
                pollEdit = pollInput;
            }
            else
            {
                MediaResolver.MediaResolution resolved = await MediaResolver.ResolveAsync(
                    edit.InputMedia!, _upload, _photos, _unitOfWork, _photoRepository, _documentsRepository);
                if (resolved.Error != null || resolved.MediaBytes == null)
                {
                    ErrorMessage failure = resolved.Error ?? ErrorMessages.MediaInvalid;
                    return Error(failure.Code, failure.Message);
                }
                mediaBytes = resolved.MediaBytes;
            }
        }

        return peer.Type == TLPeer.PeerType.PeerChannel
            ? await EditChannelMessageAsync(authKeyId, userId, peer.Id, edit,
                mediaBytes, pollEdit)
            : await EditCommonMessageAsync(authKeyId, userId, peer, edit, mediaBytes,
                pollEdit);
    }

    /// <summary>
    /// Closes the poll the addressed message carries. Every ballot already cast
    /// survives, and closing reveals the breakdown to everyone, so the media each
    /// reader ends up with still differs by which options that reader chose.
    /// Returns the closed definition plus its ballots, or the protocol error.
    /// </summary>
    private async Task<(ErrorMessage? Error, PollStore.PollSnapshot Poll,
        IReadOnlyList<PollStore.VoteSnapshot> Votes)> ClosePollAsync(
        PollStore.PollInput requested, MessageIdentity identity)
    {
        PollStore.PollSnapshot? stored = await _polls.GetAsync(identity);
        if (stored == null)
        {
            return (new ErrorMessage(400, "MESSAGE_ID_INVALID"), default,
                Array.Empty<PollStore.VoteSnapshot>());
        }
        // A close names the poll it closes, so a stale client cannot close a poll
        // that has since been replaced on the same message id.
        if (requested.RequestedId != 0 &&
            requested.RequestedId != stored.Value.PollId)
        {
            return (new ErrorMessage(400, "POLL_UNSUPPORTED"), default,
                Array.Empty<PollStore.VoteSnapshot>());
        }
        if (PollStore.IsClosed(stored.Value, UnixNow()))
        {
            return (new ErrorMessage(400, "MESSAGE_NOT_MODIFIED"), default,
                Array.Empty<PollStore.VoteSnapshot>());
        }

        PollStore.PollSnapshot closed = stored.Value with { Closed = true };
        IReadOnlyList<PollStore.VoteSnapshot> votes =
            await _polls.GetVotesAsync(closed.PollId);
        return _polls.Persist(closed, identity)
            ? (null, closed, votes)
            : (ErrorMessages.InternalServerError, default,
                Array.Empty<PollStore.VoteSnapshot>());
    }

    /// <summary>
    /// Moves or rewrites one entry of the schedule queue. The entry keeps its
    /// scheduled id, so the client updates the queue row it already has rather than
    /// gaining a second one, which is what /api/scheduled-messages requires of an
    /// `updateNewScheduledMessage` with an existing id.
    ///
    /// A date that is no longer far enough in the future is NOT silently sent here:
    /// `messages.sendScheduledMessages` is the method that flushes, and pinned TDLib
    /// routes a cleared scheduling state there
    /// (`MessagesManager.cpp:23154-23156`).
    /// </summary>
    private async Task<TLUpdates> EditScheduledMessageAsync(long authKeyId,
        long userId, DialogPeerKey peer, RequestedEdit edit)
    {
        int now = UnixNow();
        if (ScheduledMessageStore.ValidateScheduleDate(edit.ScheduleDate, now,
                peer.Type, peer.Id, userId) is { } invalid)
        {
            return Error(invalid.Code, invalid.Message);
        }
        if (!ScheduledMessageStore.IsQueued(edit.ScheduleDate, now))
        {
            return Error(400, "SCHEDULE_DATE_INVALID");
        }

        ScheduledMessageStore.ScheduledSnapshot? entry = await _scheduled.GetAsync(
            userId, peer.Type, peer.Id, edit.MessageId);
        if (entry is not { State: ScheduledMessageState.Queued })
        {
            return Error(400, "MESSAGE_ID_INVALID");
        }

        ScheduledMessageStore.ScheduledSnapshot current = entry.Value;
        if (edit.ReplacesText || edit.ReplacesEntities || edit.ReplacesReplyMarkup)
        {
            byte[] rewritten = ApplyEdit(current.MessageBytes, edit, null, now);
            if (_scheduled.ReplaceContent(current, rewritten) is not { } replaced)
            {
                return Error(500, "INTERNAL_SERVER_ERROR");
            }
            current = replaced;
        }
        if (_scheduled.Reschedule(current, edit.ScheduleDate) is not { } moved)
        {
            return Error(500, "INTERNAL_SERVER_ERROR");
        }
        if (!await _unitOfWork.SaveAsync())
        {
            return Error(500, "INTERNAL_SERVER_ERROR");
        }

        await _updates.EnqueueUpdate(userId,
            ScheduledMessageStore.BuildNewScheduledUpdate(moved),
            UpdateDeliveryScope.ExcludingAuthKeys(new[] { authKeyId }));

        byte[] updateBytes;
        using (TLUpdate update = ScheduledMessageStore.BuildNewScheduledUpdate(moved))
        {
            updateBytes = update.AsSpan().ToArray();
        }
        var userIds = new HashSet<long> { userId };
        var chatIds = new HashSet<long>();
        if (peer.Type == TLPeer.PeerType.PeerUser) userIds.Add(peer.Id);
        else chatIds.Add(peer.Id);
        List<byte[]> chats = await _fanout.GetChatBytesForViewerAsync(userId, chatIds);
        int seq = await _updatesContextFactory.GetUpdatesContext(authKeyId, userId)
            .IncrementSeq();
        _log.Debug($"⏰ Rescheduled user:{userId} peer:{peer.Type}:{peer.Id} " +
                   $"scheduled:{moved.ScheduledId} at:{moved.SendDate}");
        return _fanout.BuildUpdates(new[] { updateBytes }, userIds, chats, now, seq);
    }

    private async Task<TLUpdates> EditCommonMessageAsync(long authKeyId, long userId,
        DialogPeerKey peer, RequestedEdit edit, byte[]? mediaBytes,
        PollStore.PollInput? pollEdit)
    {
        string? accessError = await ValidateCommonPeerAsync(userId, peer);
        if (accessError != null)
        {
            return Error(accessError == "CHAT_WRITE_FORBIDDEN" ? 403 : 400,
                accessError);
        }

        StoredMessageLocation? callerCopy = await _locator.FindCommonAsync(userId,
            edit.MessageId);
        if (callerCopy == null)
        {
            return Error(400, "MESSAGE_ID_INVALID");
        }
        mediaBytes = CompleteLiveLocationEdit(mediaBytes, callerCopy.Value);

        PollStore.PollSnapshot closedPoll = default;
        IReadOnlyList<PollStore.VoteSnapshot> votes =
            Array.Empty<PollStore.VoteSnapshot>();
        if (pollEdit != null)
        {
            if (callerCopy.Value.LogicalId == null)
            {
                return Error(400, "MESSAGE_ID_INVALID");
            }
            var closed = await ClosePollAsync(pollEdit.Value,
                MessageIdentity.ForLogical(callerCopy.Value.LogicalId.Value));
            if (closed.Error is { } pollError)
            {
                return Error(pollError.Code, pollError.Message);
            }
            closedPoll = closed.Poll;
            votes = closed.Votes;
            // Only used to prove the row changes; each copy gets its own below.
            mediaBytes = PollStore.BuildMedia(closedPoll, votes, userId, UnixNow());
        }

        // Self-dialog messages have no recipient to surprise, so Telegram exempts
        // them from the ordinary edit window. Stopping a poll is exempt for a
        // different reason: it changes no content a reader already read, and a
        // poll routinely outlives the 48-hour window its message was sent in.
        bool exemptFromWindow = (peer.Type == TLPeer.PeerType.PeerUser &&
                                 peer.Id == userId) || pollEdit != null;
        string? checkError = CheckEditable(callerCopy.Value.MessageBytes, userId,
            peer, allowAdministrativeEdit: false, exemptFromWindow, edit,
            mediaBytes, out int code);
        if (checkError != null)
        {
            return Error(code, checkError);
        }

        int editDate = UnixNow();
        // Closing a poll reveals the tallies to everyone, but `chosen` stays
        // personal, so each per-owner copy is rebuilt against its own owner.
        byte[]? PerCopyMedia(StoredMessageLocation location) => pollEdit == null
            ? mediaBytes
            : PollStore.BuildMedia(closedPoll, votes, location.OwnerId, editDate);
        IReadOnlyList<StoredMessageLocation> updated = await _locator
            .MutateCommonCopiesAsync(userId, edit.MessageId,
                location => ApplyEdit(location.MessageBytes, edit,
                    PerCopyMedia(location), editDate),
                refreshSearch: true);
        if (!await _unitOfWork.SaveAsync())
        {
            return Error(500, "INTERNAL_SERVER_ERROR");
        }

        byte[]? callerUpdateBytes = null;
        foreach (StoredMessageLocation copy in updated)
        {
            IUpdatesContext context = copy.OwnerId == userId
                ? _updatesContextFactory.GetUpdatesContext(authKeyId, userId)
                : _updatesContextFactory.GetUpdatesContext(null, copy.OwnerId);
            int pts = await context.IncrementPts();
            if (copy.OwnerId == userId)
            {
                using TLUpdate callerUpdate = BuildEditUpdate(copy.MessageBytes, pts,
                    channel: false);
                callerUpdateBytes = callerUpdate.AsSpan().ToArray();
                continue;
            }

            // EnqueueUpdate owns the value it is handed, so this is a transfer.
            await _updates.EnqueueUpdate(copy.OwnerId,
                BuildEditUpdate(copy.MessageBytes, pts, channel: false));
        }
        if (callerUpdateBytes == null)
        {
            return Error(400, "MESSAGE_ID_INVALID");
        }

        var userIds = new List<long> { userId };
        if (peer.Type == TLPeer.PeerType.PeerUser)
        {
            userIds.Add(peer.Id);
        }
        List<byte[]> chats = peer.Type == TLPeer.PeerType.PeerChat
            ? await _fanout.GetChatBytesForViewerAsync(userId, new[] { peer.Id })
            : new List<byte[]>();
        int seq = await _updatesContextFactory.GetUpdatesContext(authKeyId, userId)
            .IncrementSeq();
        _log.Debug($"✏️ EditMessage user:{userId} peer:{peer.Type}:{peer.Id} " +
                   $"id:{edit.MessageId} copies:{updated.Count}");
        return _fanout.BuildUpdates(new[] { callerUpdateBytes }, userIds, chats,
            editDate, seq);
    }

    private async Task<TLUpdates> EditChannelMessageAsync(long authKeyId, long userId,
        long channelId, RequestedEdit edit, byte[]? mediaBytes,
        PollStore.PollInput? pollEdit)
    {
        if (channelId <= 0)
        {
            return Error(400, "PEER_ID_INVALID");
        }

        bool broadcast;
        byte[] channelBytes;
        using (TLChat? chat = await _chatRepository.GetChatAsync(channelId))
        {
            if (chat == null || chat.Value.Type != TLChat.ChatType.Channel)
            {
                return Error(400, "PEER_ID_INVALID");
            }
            broadcast = chat.Value.AsChannel().Broadcast;
            channelBytes = chat.Value.AsSpan().ToArray();
        }

        bool canAdministrativelyEdit;
        using (TLChatParticipantInfo? participant = await _chatParticipantsRepository.GetParticipantAsync(channelId, userId))
        {
            if (participant == null ||
                !MessageEditRules.IsActiveParticipant(participant.Value))
            {
                return Error(403, "CHAT_WRITE_FORBIDDEN");
            }
            canAdministrativelyEdit = broadcast &&
                                      MessageEditRules.HasEditMessagesRight(
                                          participant.Value);
            bool isAdmin = ChatRights.HasAdminRight(participant.Value,
                ChatAdminRightRequirement.Any);
            if (!isAdmin &&
                (ChatRights.IsRestrictedFrom(participant.Value,
                     ChatBannedAction.SendMessages, UnixNow()) ||
                 ChatRights.DefaultBans(channelBytes, ChatBannedAction.SendMessages)))
            {
                return Error(403, "CHAT_WRITE_FORBIDDEN");
            }
        }

        StoredMessageLocation? stored = await _locator.FindChannelAsync(channelId,
            edit.MessageId);
        if (stored == null)
        {
            return Error(400, "MESSAGE_ID_INVALID");
        }
        mediaBytes = CompleteLiveLocationEdit(mediaBytes, stored.Value);

        PollStore.PollSnapshot closedPoll = default;
        IReadOnlyList<PollStore.VoteSnapshot> votes =
            Array.Empty<PollStore.VoteSnapshot>();
        if (pollEdit != null)
        {
            var closed = await ClosePollAsync(pollEdit.Value,
                MessageIdentity.ForChannel(channelId, edit.MessageId));
            if (closed.Error is { } pollError)
            {
                return Error(pollError.Code, pollError.Message);
            }
            closedPoll = closed.Poll;
            votes = closed.Votes;
            // A channel post exists once and cannot carry one member's `chosen`
            // flags, so the shared row stores the neutral, voter-less view.
            mediaBytes = PollStore.BuildMedia(closedPoll, votes, 0, UnixNow());
        }

        var peer = new DialogPeerKey(TLPeer.PeerType.PeerChannel, channelId);
        string? checkError = CheckEditable(stored.Value.MessageBytes, userId, peer,
            canAdministrativelyEdit, canAdministrativelyEdit || pollEdit != null,
            edit, mediaBytes, out int code);
        if (checkError != null)
        {
            // A broadcast post the caller neither authored nor may administer is
            // reported as missing admin rights rather than as a wrong author.
            if (checkError == "MESSAGE_AUTHOR_REQUIRED" && broadcast)
            {
                return Error(400, "CHAT_ADMIN_REQUIRED");
            }
            return Error(code, checkError);
        }

        int editDate = UnixNow();
        StoredMessageLocation? updated = await _locator.MutateChannelAsync(channelId,
            edit.MessageId,
            location => ApplyEdit(location.MessageBytes, edit, mediaBytes, editDate),
            refreshSearch: true);
        if (updated == null)
        {
            return Error(400, "MESSAGE_ID_INVALID");
        }
        if (!await _unitOfWork.SaveAsync())
        {
            return Error(500, "INTERNAL_SERVER_ERROR");
        }

        var channelBox = new ChannelMessageBox(_counterFactory, channelId);
        int pts = await channelBox.IncrementPts();
        List<long> memberIds = await _fanout.GetOtherActiveChannelMemberIdsAsync(
            channelId, userId);

        // A closed poll's results are no longer `min`, and pinned TDLib takes a
        // non-min result as authoritative for `chosen`
        // (`PollManager.cpp:1769`). Delivering the neutral shared row to a member
        // who voted would therefore erase their own answer from their client, so
        // each member's update carries the poll as that member sees it.
        byte[] ViewerMessageBytes(long viewerId)
        {
            byte[] bytes = updated.Value.MessageBytes;
            if (pollEdit == null)
            {
                return bytes;
            }
            using var row = new TLMessage(bytes, 0, bytes.Length);
            using TLMessage rebuilt = MessageRows.RebuildMedia(row.AsMessage(),
                PollStore.BuildMedia(closedPoll, votes, viewerId, editDate));
            return rebuilt.AsSpan().ToArray();
        }

        foreach (long memberId in memberIds)
        {
            await _updates.EnqueueUpdate(memberId,
                BuildEditUpdate(ViewerMessageBytes(memberId), pts, channel: true));
        }

        byte[] callerUpdateBytes;
        using (TLUpdate callerUpdate = BuildEditUpdate(ViewerMessageBytes(userId),
                   pts, channel: true))
        {
            callerUpdateBytes = callerUpdate.AsSpan().ToArray();
        }
        int seq = await _updatesContextFactory.GetUpdatesContext(authKeyId, userId)
            .IncrementSeq();
        _log.Debug($"✏️ EditMessage user:{userId} channel:{channelId} " +
                   $"id:{edit.MessageId} pts:{pts} members:{memberIds.Count}");
        return _fanout.BuildUpdates(new[] { callerUpdateBytes }, new[] { userId },
            new[] { channelBytes }, editDate, seq);
    }

    /// <summary>
    /// Finishes a live-location edit against the row being edited. Stopping a
    /// live location carries no position and no period, so the resolved media
    /// takes the row's last known point and a period equal to the time it was
    /// actually live. Every other media edit passes through untouched.
    /// </summary>
    private byte[]? CompleteLiveLocationEdit(byte[]? mediaBytes,
        StoredMessageLocation location)
    {
        if (mediaBytes == null)
        {
            return null;
        }

        byte[] storedBytes = location.MessageBytes;
        using var stored = new TLMessage(storedBytes, 0, storedBytes.Length);
        if (stored.Type != TLMessage.MessageType.Message)
        {
            return mediaBytes;
        }
        var message = stored.AsMessage();
        return MediaResolver.ApplyLiveLocationStop(mediaBytes, message.Media,
            message.Date, UnixNow());
    }

    /// <summary>
    /// Rejects an edit that the caller may not perform or that would change
    /// nothing. The stored bytes are read synchronously so no ref-struct view
    /// crosses an await.
    /// </summary>
    private string? CheckEditable(byte[] storedBytes, long userId,
        DialogPeerKey requestedPeer, bool allowAdministrativeEdit,
        bool exemptFromWindow, RequestedEdit edit, byte[]? mediaBytes,
        out int code)
    {
        code = 400;
        using var stored = new TLMessage(storedBytes, 0, storedBytes.Length);
        if (stored.Type != TLMessage.MessageType.Message ||
            !MessageStore.TryReadStoredMessageInfo(stored, out StoredMessageInfo info) ||
            info.PeerType != requestedPeer.Type || info.PeerId != requestedPeer.Id)
        {
            return "MESSAGE_ID_INVALID";
        }

        var message = stored.AsMessage();
        if (!MessageEditRules.IsAuthoredBy(message, userId) &&
            !allowAdministrativeEdit)
        {
            code = 403;
            return "MESSAGE_AUTHOR_REQUIRED";
        }
        if (MessageEditRules.IsExpired(message, UnixNow(), exemptFromWindow))
        {
            return "MESSAGE_EDIT_TIME_EXPIRED";
        }
        return Changes(message, edit, mediaBytes) ? null : "MESSAGE_NOT_MODIFIED";
    }

    private static bool Changes(Message message, RequestedEdit edit,
        byte[]? mediaBytes)
    {
        if (edit.ReplacesText &&
            !message.MessageProperty.SequenceEqual(edit.Text))
        {
            return true;
        }
        if (edit.ReplacesEntities &&
            !message.Entities.ToReadOnlySpan().SequenceEqual(edit.Entities))
        {
            return true;
        }
        // Replacing the text without entities drops the ones the row still holds.
        if (edit.ReplacesText && !edit.ReplacesEntities && message.Flags[7])
        {
            return true;
        }
        if (edit.ReplacesMedia && mediaBytes != null &&
            !message.Media.SequenceEqual(mediaBytes))
        {
            return true;
        }
        return edit.ReplacesReplyMarkup &&
               !message.ReplyMarkup.SequenceEqual(edit.ReplyMarkup);
    }

    private static byte[] ApplyEdit(byte[] storedBytes, RequestedEdit edit,
        byte[]? mediaBytes, int editDate)
    {
        using var stored = new TLMessage(storedBytes, 0, storedBytes.Length);
        var entities = edit.ReplacesEntities
            ? new Vector(edit.Entities.AsSpan())
            : default;
        using TLMessage rebuilt = MessageRows.RebuildEdited(stored.AsMessage(),
            edit.Text, edit.ReplacesText, entities, edit.ReplacesEntities,
            mediaBytes ?? Array.Empty<byte>(), edit.ReplacesMedia,
            edit.ReplyMarkup, edit.ReplacesReplyMarkup, editDate);
        return rebuilt.AsSpan().ToArray();
    }

    private static TLUpdate BuildEditUpdate(byte[] messageBytes, int pts,
        bool channel) => channel
        ? UpdateEditChannelMessage.Builder()
            .Message(messageBytes)
            .Pts(pts)
            .PtsCount(1)
            .Build()
        : UpdateEditMessage.Builder()
            .Message(messageBytes)
            .Pts(pts)
            .PtsCount(1)
            .Build();

    private async ValueTask<string?> ValidateCommonPeerAsync(long userId,
        DialogPeerKey peer)
    {
        if (peer.Id <= 0)
        {
            return "PEER_ID_INVALID";
        }
        if (peer.Type == TLPeer.PeerType.PeerUser)
        {
            using TLUser? user = _userRepository.GetUser(peer.Id);
            return user == null ? "PEER_ID_INVALID" : null;
        }
        if (peer.Type != TLPeer.PeerType.PeerChat)
        {
            return "PEER_ID_INVALID";
        }

        byte[] chatBytes;
        using (TLChat? chat = await _chatRepository.GetChatAsync(peer.Id))
        {
            if (chat == null || chat.Value.Type != TLChat.ChatType.Chat ||
                chat.Value.AsChat().Deactivated)
            {
                return "PEER_ID_INVALID";
            }
            chatBytes = chat.Value.AsSpan().ToArray();
        }

        using TLChatParticipantInfo? participant = await _chatParticipantsRepository.GetParticipantAsync(peer.Id, userId);
        if (participant == null ||
            !MessageEditRules.IsActiveParticipant(participant.Value))
        {
            return "CHAT_WRITE_FORBIDDEN";
        }
        bool isAdmin = ChatRights.HasAdminRight(participant.Value,
            ChatAdminRightRequirement.Any);
        if (!isAdmin &&
            (ChatRights.IsRestrictedFrom(participant.Value,
                 ChatBannedAction.SendMessages, UnixNow()) ||
             ChatRights.DefaultBans(chatBytes, ChatBannedAction.SendMessages)))
        {
            return "CHAT_WRITE_FORBIDDEN";
        }
        return null;
    }

    // Await-safe snapshot of the request; every ref-struct view is read here.
    private sealed record RequestedEdit(int MessageId, byte[] Text, bool ReplacesText,
        byte[] Entities, bool ReplacesEntities, byte[]? InputMedia, bool ReplacesMedia,
        byte[] ReplyMarkup, bool ReplacesReplyMarkup, bool Scheduled,
        int ScheduleDate, bool QuickReply);

    private static RequestedEdit ReadRequestedEdit(EditMessage request)
    {
        Flags flags = request.Flags;
        return new RequestedEdit(
            request.Id,
            flags[11] ? request.Message.ToArray() : Array.Empty<byte>(), flags[11],
            flags[3] ? request.Entities.ToReadOnlySpan().ToArray() : Array.Empty<byte>(),
            flags[3],
            flags[14] ? request.Media.ToArray() : null, flags[14],
            flags[2] ? request.ReplyMarkup.ToArray() : Array.Empty<byte>(), flags[2],
            flags[15], flags[15] ? request.ScheduleDate : 0, flags[17]);
    }

    private int UnixNow() =>
        checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());

    private static TLUpdates Error(int code, string message) =>
        (TLUpdates)RpcErrorGenerator.GenerateError(code,
            Encoding.UTF8.GetBytes(message));
}
