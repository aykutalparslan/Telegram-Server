// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

/// <summary>
/// State of one entry in a durable scheduled-message queue. A row is deleted once
/// it has been flushed or dropped, so only these two states are ever stored.
/// </summary>
public static class ScheduledMessageState
{
    /// Waiting for its send date, a manual flush, or a delete.
    public const int Queued = 0;

    /// <summary>
    /// A flush has taken ownership of the row and no other flush may send it. The
    /// claim is the whole of the at-most-once guarantee: a timer tick, a manual
    /// `messages.sendScheduledMessages` and a restart all have to pass through it.
    /// </summary>
    public const int Claimed = 1;
}

/// <summary>
/// The scheduled-message queue. Rows are keyed by the scheduling user AND the
/// destination dialog, because /api/scheduled-messages gives every dialog its own
/// scheduled id sequence, and by scheduled id within that dialog.
///
/// There is no send-date index. A due pass iterates the whole table, which is
/// bounded by the number of messages currently queued across the deployment
/// rather than by history size, and it keeps the row's identity stable across a
/// reschedule: a send-date-prefixed key would have to be deleted and re-inserted
/// every time a client moves a message, which is exactly where a stale index
/// entry would leave a queue entry that can never be flushed.
/// </summary>
public interface IScheduledMessagesRepository
{
    bool PutScheduledMessage(TLScheduledMessage message);

    ValueTask<TLScheduledMessage?> GetScheduledMessageAsync(long ownerUserId,
        int peerType, long peerId, int scheduledId);

    /// Every entry of one dialog's queue, in no particular order.
    ValueTask<IReadOnlyCollection<TLScheduledMessage>> GetScheduledMessagesAsync(
        long ownerUserId, int peerType, long peerId);

    /// <summary>
    /// Every queued entry across the deployment. Used by the due coordinator and
    /// by startup reconciliation, which must also see rows left <see
    /// cref="ScheduledMessageState.Claimed"/> by a process that died mid-flush.
    /// </summary>
    ValueTask<IReadOnlyCollection<TLScheduledMessage>> GetAllScheduledMessagesAsync();

    bool DeleteScheduledMessage(long ownerUserId, int peerType, long peerId,
        int scheduledId);
}
