// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

/// <summary>
/// The append-only per-channel administrative event ledger `channels.getAdminLog`
/// reads.
///
/// The ledger only ever reports what Ferrite actually recorded, so there is no
/// update or delete of an event: an administrative action appends one row in the
/// same change that performs the mutation, and nothing rewrites it afterwards.
/// Rows are keyed `(channel_id, id)` with a per-channel monotonically increasing
/// id, which is also the paging cursor `max_id`/`min_id` names.
///
/// The row is `dto.adminLogEvent` rather than `dto.channelAdminLogEvent` on
/// purpose: the wire type `channelAdminLogEvent#1fad68cd` already generates
/// `ChannelAdminLogEvent`, and a dto of the same identifier would collide with it
/// in every file that uses both namespaces.
/// </summary>
public interface IChannelAdminLogRepository
{
    public bool PutEvent(TLAdminLogEvent row);

    /// <summary>
    /// Every recorded event for one channel. Ordering is the caller's business:
    /// `channels.getAdminLog` answers newest first and pages by event id, and the
    /// shared storage contract only guarantees prefix ITERATION, not sort order.
    /// Each returned value is owned by the caller.
    /// </summary>
    public ValueTask<IReadOnlyCollection<TLAdminLogEvent>> GetEventsAsync(
        long channelId);
}
