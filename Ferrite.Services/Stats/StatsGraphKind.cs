// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Stats;

/// <summary>
/// The graphs Ferrite can serve, one value per `StatsGraph` slot in
/// `stats.broadcastStats`, `stats.megagroupStats` and `stats.messageStats`.
///
/// EVERY graph in those three answers is handed out as a `statsGraphAsync` token
/// and computed only when `stats.loadAsyncGraph` asks for it. That is what the
/// placeholder exists for — "certain graphs are not directly sent [...] to reduce
/// server load" (https://core.telegram.org/api/stats) — and it keeps the
/// statistics answer a cheap set of counters.
///
/// These values are PERSISTED in `dto.statsGraphToken.graph`, so a value is never
/// reused for a different graph and a removed graph's value is never recycled.
/// </summary>
public enum StatsGraphKind
{
    ChannelGrowth = 1,
    ChannelFollowers = 2,
    ChannelMute = 3,
    ChannelTopHours = 4,
    ChannelInteractions = 5,
    ChannelInstantViewInteractions = 6,
    ChannelViewsBySource = 7,
    ChannelNewFollowersBySource = 8,
    ChannelLanguages = 9,
    ChannelReactionsByEmotion = 10,
    ChannelStoryInteractions = 11,
    ChannelStoryReactionsByEmotion = 12,
    GroupGrowth = 13,
    GroupMembers = 14,
    GroupNewMembersBySource = 15,
    GroupLanguages = 16,
    GroupMessages = 17,
    GroupActions = 18,
    GroupTopHours = 19,
    GroupWeekdays = 20,
    MessageViews = 21,
    MessageReactionsByEmotion = 22,
}
