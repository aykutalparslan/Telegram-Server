// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

/// <summary>
/// The two durable rows `stats.*` needs that no other repository already keeps.
///
/// Everything else Ferrite reports as statistics is derived at read time from
/// rows that already exist for their own reasons — participants, channel
/// messages, view receipts, reactions and the administrative ledger — so nothing
/// here duplicates a counter that lives elsewhere.
///
/// The FORWARD INDEX is keyed by the SOURCE post, because that is the direction
/// `stats.getMessagePublicForwards` reads it in: given a channel post, which
/// public channels re-posted it. A forward into a private destination is not
/// indexed at all, so the index is exactly the set of forwards a stranger could
/// also have found.
///
/// A GRAPH TOKEN is the server's promise to serve one specific graph. It is
/// keyed by the token itself, so a token Ferrite never issued has no row and is
/// refused rather than answered with an empty graph.
/// </summary>
public interface IStatisticsRepository
{
    /// <summary>
    /// Records one public forward. Re-forwarding the same post into the same
    /// destination message is the same row, so the index never double-counts.
    /// </summary>
    bool PutPublicForward(TLPublicForwardRef row);

    /// <summary>
    /// Every recorded public forward of one channel post. Ordering is the
    /// caller's business: the shared storage contract only guarantees prefix
    /// ITERATION. Each returned value is owned by the caller.
    /// </summary>
    ValueTask<IReadOnlyCollection<TLPublicForwardRef>> GetPublicForwardsAsync(
        long channelId, int msgId);

    bool PutGraphToken(TLStatsGraphToken row);

    /// <summary>
    /// The graph a token names, or null when Ferrite never issued it. The value
    /// is owned by the caller.
    /// </summary>
    ValueTask<TLStatsGraphToken?> GetGraphTokenAsync(string token);
}
