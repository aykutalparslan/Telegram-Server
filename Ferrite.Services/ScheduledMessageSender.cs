// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services;

/// <summary>
/// The scheduling half of the send paths. `messages.sendMessage`,
/// `messages.sendMedia` and `messages.sendMultiMedia` all normalize their request to
/// a `messages.sendMessage` shape, so one entry point turns any of them into queue
/// entries and the `Updates` the client expects back.
///
/// No recipient learns anything here: the whole point of the queue is that nothing is
/// delivered until the entry is flushed.
/// </summary>
public sealed class ScheduledMessageSender
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ScheduledMessageStore _scheduled;
    private readonly IUpdatesService _updates;
    private readonly UpdateFanout _fanout;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly ILogger _log;
    private readonly DraftStore _drafts;

    public ScheduledMessageSender(IUnitOfWork unitOfWork,
        ScheduledMessageStore scheduled, IUpdatesService updates,
        UpdateFanout fanout, IUpdatesContextFactory updatesContextFactory,
        ILogger log, DraftStore drafts)
    {
        _unitOfWork = unitOfWork;
        _scheduled = scheduled;
        _updates = updates;
        _fanout = fanout;
        _updatesContextFactory = updatesContextFactory;
        _log = log;
        _drafts = drafts;
    }

    /// One item of the request: its normalized send request plus its resolved media.
    public readonly record struct ScheduledItem(byte[] SendMessageBytes,
        byte[]? MediaBytes);

    public bool IsQueued(int scheduleDate) =>
        ScheduledMessageStore.IsQueued(scheduleDate, _scheduled.UnixNow());

    /// <summary>
    /// Stores one queue entry per item and answers with the `updateMessageID` plus
    /// `updateNewScheduledMessage` pair the pinned client requires: it verifies that
    /// a send result carries exactly one new message and one matching random id
    /// (`MessagesManager.cpp:26885-26907`), and resolves a scheduled id through the
    /// `updateMessageID` (`MessagesManager.cpp:24847`).
    /// </summary>
    public async Task<TLUpdates> ScheduleAsync(long authKeyId, long userId,
        PreparedMessageTarget target, IReadOnlyList<ScheduledItem> items,
        long groupedId, int scheduleDate)
    {
        int now = _scheduled.UnixNow();
        if (ScheduledMessageStore.ValidateScheduleDate(scheduleDate, now,
                target.PeerType, target.PeerId, userId) is { } invalid)
        {
            return Error(invalid.Code, invalid.Message);
        }

        bool channel = target.PeerType == TLPeer.PeerType.PeerChannel;
        var entries = new List<ScheduledMessageStore.ScheduledSnapshot>(items.Count);
        foreach (ScheduledItem item in items)
        {
            long randomId;
            byte[] templateBytes;
            using (var request = new TLBytes(item.SendMessageBytes, 0,
                       item.SendMessageBytes.Length))
            {
                randomId = ((SendMessage)request).RandomId;
                using TLPeer from = PeerResolver.BuildPeer(target.Sender.Type,
                    target.Sender.Id);
                using TLPeer to = PeerResolver.BuildPeer(target.PeerType,
                    target.PeerId);
                using TLMessage template = SendPipeline.BuildScheduledTemplate(
                    request, scheduledId: 0, from, to, scheduleDate,
                    outgoing: !channel, post: channel && target.Broadcast,
                    forumTopic: target.ForumTopic != null && target.ForumTopicId != 1,
                    media: item.MediaBytes, groupedId: groupedId);
                templateBytes = template.AsSpan().ToArray();
            }

            ScheduledMessageStore.ScheduledSnapshot? entry = await _scheduled
                .EnqueueAsync(userId, target.PeerType, target.PeerId, randomId,
                    scheduleDate, templateBytes);
            if (entry == null)
            {
                return Error(400, "SCHEDULE_TOO_MUCH");
            }
            entries.Add(entry.Value);
        }

        if (!await _unitOfWork.SaveAsync())
        {
            return Error(500, "INTERNAL_SERVER_ERROR");
        }
        if (items.Count > 0 &&
            !await _drafts.ClearAfterSendAsync(authKeyId, userId, target.PeerType,
                target.PeerId, items[0].SendMessageBytes))
        {
            _log.Warning($"Could not clear scheduled-send draft for " +
                         $"{target.PeerType}:{target.PeerId} user:{userId}");
        }

        var updateBytes = new List<byte[]>(entries.Count * 2);
        var userIds = new HashSet<long> { userId };
        var chatIds = new HashSet<long>();
        foreach (ScheduledMessageStore.ScheduledSnapshot entry in entries)
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

            // The queue belongs to the user, not to the session that filled it, so
            // the account's other sessions see the same entry appear.
            await _updates.EnqueueUpdate(userId,
                ScheduledMessageStore.BuildNewScheduledUpdate(entry),
                UpdateDeliveryScope.ExcludingAuthKeys(new[] { authKeyId }));
        }

        if (target.PeerType == TLPeer.PeerType.PeerUser)
        {
            userIds.Add(target.PeerId);
        }
        else
        {
            chatIds.Add(target.PeerId);
        }
        List<byte[]> chats = await _fanout.GetChatBytesForViewerAsync(userId, chatIds);
        int seq = await _updatesContextFactory.GetUpdatesContext(authKeyId, userId)
            .IncrementSeq();
        _log.Debug($"⏰ Scheduled {entries.Count} message(s) user:{userId} " +
                   $"peer:{target.PeerType}:{target.PeerId} at:{scheduleDate}");
        return _fanout.BuildUpdates(updateBytes, userIds, chats, now, seq);
    }

    private static TLUpdates Error(int code, string message) =>
        (TLUpdates)RpcErrorGenerator.GenerateError(code,
            Encoding.UTF8.GetBytes(message));
}
