// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;

namespace Ferrite.Services.Scheduling;

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

    public readonly record struct FlushedMessage(int ScheduledId, int SentMessageId,
        int Pts, int Date, byte[] SenderMessageBytes, bool Channel,
        byte[]? ChatBytes);

    public readonly record struct FlushOutcome(ErrorMessage? Error,
        FlushedMessage? Message)
    {
        public static FlushOutcome Fail(int code, string message) =>
            new(new ErrorMessage(code, message), null);
    }

    public async Task<FlushOutcome> FlushAsync(long? authKeyId,
        ScheduledMessageStore.ScheduledSnapshot snapshot, int now)
    {
        ScheduledMessageStore.ScheduledSnapshot? claimed =
            await _scheduled.TryClaimAsync(snapshot);
        if (claimed == null)
        {
            return FlushOutcome.Fail(400, "MESSAGE_ID_INVALID");
        }

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

        _scheduled.Delete(claimed.Value);
        await _unitOfWork.SaveAsync();
        _log.Debug($"⏰ Scheduled message flushed user:{claimed.Value.OwnerUserId} " +
                   $"peer:{claimed.Value.PeerType}:{claimed.Value.PeerId} " +
                   $"scheduled:{flushed.ScheduledId} sent:{flushed.SentMessageId}");
        return new FlushOutcome(null, flushed);
    }

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

    private static byte[] ForSending(byte[] messageBytes, int now)
    {
        using var stored = new TLMessage(messageBytes, 0, messageBytes.Length);
        using TLMessage sending = stored.AsMessage().Clone()
            .Date(now)
            .FromScheduled(true)
            .Build();
        return sending.AsSpan().ToArray();
    }

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
