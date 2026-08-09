// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Stats;

/// <summary>What a recorded administrative action counts as in statistics.</summary>
public enum StatsAdminActionKind
{
    /// <summary>A message the administrator deleted.</summary>
    Deleted,

    /// <summary>A member the administrator removed from the channel.</summary>
    Kicked,

    /// <summary>A member the administrator restricted while leaving them in.</summary>
    Banned,
}

/// <summary>One current member, as statistics see them.</summary>
public readonly record struct StatsMember(long UserId, long InviterId, int Date);

/// <summary>One stored message in the channel's shared box.</summary>
public readonly record struct StatsMessage(int Id, int Date, long SenderUserId,
    int Length);

/// <summary>
/// One recorded view of one post, by one viewer, at one time. A viewer has at
/// most one receipt per post, so the receipts of a post ARE its view count — the
/// same rule `messages.getMessagesViews` maintains its counter by.
/// </summary>
public readonly record struct StatsView(int MessageId, long UserId, int Date);

/// <summary>One reaction on one post.</summary>
public readonly record struct StatsReaction(int MessageId, int Date, string Emoticon);

/// <summary>One recorded forward of one post into a public channel.</summary>
public readonly record struct StatsForward(int MessageId, int Date);

/// <summary>One recorded administrative action, credited to its actor.</summary>
public readonly record struct StatsAdminAction(long UserId, int Date,
    StatsAdminActionKind Kind);

/// <summary>
/// Everything statistics are derived from, read once per request.
///
/// Nothing in here is a statistics-specific counter: every field is a projection
/// of rows Ferrite already keeps for their own reasons — membership, the shared
/// channel message box, view receipts, reactions, the public-forward index and
/// the administrative ledger. That is what makes the answers truthful, and it is
/// also why a graph Ferrite has no rows for comes back EMPTY rather than
/// fabricated: there is simply no projection to build it from.
/// </summary>
public sealed record ChannelStatsSnapshot(
    IReadOnlyList<StatsMember> Members,
    IReadOnlyList<StatsMessage> Messages,
    IReadOnlyList<StatsView> Views,
    IReadOnlyList<StatsReaction> Reactions,
    IReadOnlyList<StatsForward> Forwards,
    IReadOnlyList<StatsAdminAction> AdminActions)
{
    public static ChannelStatsSnapshot Empty { get; } = new([], [], [], [], [], []);
}
