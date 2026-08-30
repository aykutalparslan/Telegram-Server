// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Stats;

public enum StatsAdminActionKind
{
    Deleted,

    Kicked,

    Banned,
}

public readonly record struct StatsMember(long UserId, long InviterId, int Date);

public readonly record struct StatsMessage(int Id, int Date, long SenderUserId,
    int Length);

public readonly record struct StatsView(int MessageId, long UserId, int Date);

public readonly record struct StatsReaction(int MessageId, int Date, string Emoticon);

public readonly record struct StatsForward(int MessageId, int Date);

public readonly record struct StatsAdminAction(long UserId, int Date,
    StatsAdminActionKind Kind);

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
