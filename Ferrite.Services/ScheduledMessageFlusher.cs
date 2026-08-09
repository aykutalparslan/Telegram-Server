// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;

namespace Ferrite.Services;

/// <summary>
/// Sends one claimed scheduled entry through the ordinary send pipeline. Every way
/// a queue entry can leave the queue by being sent goes through here: the manual
/// `messages.sendScheduledMessages`, the due coordinator's timer, and the
/// status-change flush of a `when online` entry.
///
/// The sent row carries `from_scheduled`, which is how the scheduling client learns
/// its own queued message went out (/api/scheduled-messages); the recipients' copies
/// do not, because the flag names the sender's queue.
/// </summary>
public sealed class ScheduledMessageFlusher
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IForumTopicsRepository _forumTopicsRepository;
    private readonly IMessagingSettingsRepository _messagingSettingsRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ScheduledMessageStore _scheduled;
    private readonly SendPipeline _send;
    private readonly IUpdatesService _updates;
    private readonly ILogger _log;

    public ScheduledMessageFlusher(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository,
        ScheduledMessageStore scheduled, SendPipeline send, IUpdatesService updates,
        ILogger log)
    {
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _forumTopicsRepository = forumTopicsRepository;
        _messagingSettingsRepository = messagingSettingsRepository;

        _unitOfWork = unitOfWork;
        _scheduled = scheduled;
        _send = send;
        _updates = updates;
        _log = log;
    }

    /// <summary>
    /// One flushed entry: the scheduled id that left the queue, the real id it
    /// became, and the sender's own copy of the row so the caller can build or
    /// enqueue that sender's `updateNewMessage`.
    /// </summary>
    public readonly record struct FlushedMessage(int ScheduledId, int SentMessageId,
        int Pts, int Date, byte[] SenderMessageBytes, bool Channel,
        byte[]? ChatBytes);

    public readonly record struct FlushOutcome(ErrorMessage? Error,
        FlushedMessage? Message)
    {
        public static FlushOutcome Fail(int code, string message) =>
            new(new ErrorMessage(code, message), null);
    }

    /// <summary>
    /// Claims and sends one entry. A lost claim is reported as a missing message id
    /// rather than as an error, because the only ways to lose it are that another
    /// flush already sent this entry or that it was deleted.
    /// </summary>
    public async Task<FlushOutcome> FlushAsync(long? authKeyId,
        ScheduledMessageStore.ScheduledSnapshot snapshot, int now)
    {
        ScheduledMessageStore.ScheduledSnapshot? claimed =
            await _scheduled.TryClaimAsync(snapshot);
        if (claimed == null)
        {
            return FlushOutcome.Fail(400, "MESSAGE_ID_INVALID");
        }

        // Rights are re-checked at flush time, not at schedule time: a user can be
        // removed from a chat, or lose posting rights, between the two.
        DialogPeerKey? queuedSender = claimed.Value.PeerType ==
                                      TLPeer.PeerType.PeerChannel
            ? ReadSender(claimed.Value.MessageBytes)
            : null;
        PreparedMessageTarget target = await MessageSendTargetResolver.PrepareAsync(_chatRepository, _chatParticipantsRepository, _forumTopicsRepository, _messagingSettingsRepository, claimed.Value.OwnerUserId, claimed.Value.PeerType,
            claimed.Value.PeerId, Array.Empty<byte>(),
            new[] { ChatBannedAction.SendMessages },
            explicitForumTopicId: ReadForumTopicId(claimed.Value.MessageBytes),
            explicitSender: queuedSender,
            hasExplicitSender: claimed.Value.PeerType ==
                               TLPeer.PeerType.PeerChannel);
        if (target.Error != null)
        {
            // Nothing was sent, so the claim is released and the entry goes back
            // into the queue. Leaving it claimed would be worse than either
            // alternative: the client would still see the entry while nothing could
            // ever flush it again. A user who was removed from a chat keeps their
            // queued message and can delete it; one whose rights come back can send
            // it. Debug, not warning: a due entry in a dialog the owner can no
            // longer post to is re-checked on every scan.
            await _scheduled.ReleaseClaimAsync(claimed.Value);
            _log.Debug($"⏰ Scheduled flush refused user:{claimed.Value.OwnerUserId} " +
                       $"peer:{claimed.Value.PeerType}:{claimed.Value.PeerId} " +
                       $"scheduled:{claimed.Value.ScheduledId} error:{target.Error}");
            return FlushOutcome.Fail(403, target.Error);
        }

        byte[] template = ForSending(claimed.Value.MessageBytes, now);
        FlushedMessage flushed;
        if (target.PeerType == TLPeer.PeerType.PeerChannel)
        {
            ChannelSentBatch batch = await _send.SendPreparedChannelMessageAsync(
                claimed.Value.OwnerUserId, target.PeerId, target.ForumTopicId,
                target.ForumTopic, template, target.ChatBytes!,
                claimed.Value.RandomId, deriveMentions: !target.Broadcast);
            flushed = new FlushedMessage(claimed.Value.ScheduledId, batch.Id,
                batch.Pts, batch.Date, batch.MessageBytes, true, batch.ChannelBytes);
        }
        else
        {
            ShortSentBatch batch = target.PeerType == TLPeer.PeerType.PeerChat
                ? await _send.SendPreparedBasicGroupMessageAsync(authKeyId ?? 0,
                    claimed.Value.OwnerUserId, target.PeerId, target.RelatedUserIds,
                    template, claimed.Value.RandomId, target.ChatBytes,
                    deriveMentions: true)
                : await _send.SendPreparedPrivateMessageAsync(authKeyId ?? 0,
                    claimed.Value.OwnerUserId, target.PeerType, target.PeerId,
                    template, claimed.Value.RandomId);
            flushed = new FlushedMessage(claimed.Value.ScheduledId, batch.Id,
                batch.Pts, batch.Date, batch.MessageBytes, false, batch.ChatBytes);
        }

        // The entry is removed only after the send committed, so a crash before
        // this point leaves a claimed row that reconciliation reports rather than
        // an entry that could be sent a second time.
        _scheduled.Delete(claimed.Value);
        await _unitOfWork.SaveAsync();
        _log.Debug($"⏰ Scheduled message flushed user:{claimed.Value.OwnerUserId} " +
                   $"peer:{claimed.Value.PeerType}:{claimed.Value.PeerId} " +
                   $"scheduled:{flushed.ScheduledId} sent:{flushed.SentMessageId}");
        return new FlushOutcome(null, flushed);
    }

    /// <summary>
    /// Delivers a flush the owner did not request, so the owner's own sessions learn
    /// about it the same way a peer does. `EnqueueUpdate` owns the value it is
    /// handed, so both updates are transferred.
    /// </summary>
    public async Task PublishAsync(ScheduledMessageStore.ScheduledSnapshot snapshot,
        FlushedMessage flushed)
    {
        await _updates.EnqueueUpdate(snapshot.OwnerUserId,
            BuildNewMessageUpdate(flushed));
        await _updates.EnqueueUpdate(snapshot.OwnerUserId,
            ScheduledMessageStore.BuildDeleteScheduledUpdate(snapshot.PeerType,
                snapshot.PeerId, new[] { flushed.ScheduledId },
                new[] { flushed.SentMessageId }));
    }

    public static TLUpdate BuildNewMessageUpdate(FlushedMessage flushed) =>
        flushed.Channel
            ? UpdateNewChannelMessage.Builder()
                .Message(flushed.SenderMessageBytes)
                .Pts(flushed.Pts)
                .PtsCount(1)
                .Build()
            : UpdateNewMessage.Builder()
                .Message(flushed.SenderMessageBytes)
                .Pts(flushed.Pts)
                .PtsCount(1)
                .Build();

    /// <summary>
    /// Turns a queue row back into an ordinary outgoing message: the date becomes
    /// the moment it is actually sent, and `from_scheduled` marks where it came
    /// from. Both are expressible through a builder clone, because `date` is a plain
    /// field and `from_scheduled` is a bare `flags.18?true`.
    /// </summary>
    private static byte[] ForSending(byte[] messageBytes, int now)
    {
        using var stored = new TLMessage(messageBytes, 0, messageBytes.Length);
        using TLMessage sending = stored.AsMessage().Clone()
            .Date(now)
            .FromScheduled(true)
            .Build();
        return sending.AsSpan().ToArray();
    }

    // A queue row is a `message`, so its destination topic is in its own reply
    // header rather than in a request field. This is the same resolution stored
    // channel rows already use, so a queued post lands in the topic it named.
    private static int ReadForumTopicId(byte[] messageBytes)
    {
        using var stored = new TLMessage(messageBytes, 0, messageBytes.Length);
        return ForumMessages.ResolveStoredForumTopicId(stored.AsSpan(),
            stored.Type == TLMessage.MessageType.Message
                ? stored.AsMessage().Id
                : 0);
    }

    private static DialogPeerKey? ReadSender(byte[] messageBytes)
    {
        using var stored = new TLMessage(messageBytes, 0, messageBytes.Length);
        if (stored.Type != TLMessage.MessageType.Message)
        {
            return null;
        }
        var message = stored.AsMessage();
        if (!message.Flags[8] || !PeerResolver.TryReadPeer(
                message.Get_FromIdView(), out var sender))
        {
            return null;
        }
        return new DialogPeerKey(sender.Type, sender.Id);
    }
}
