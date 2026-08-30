// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Stats;

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
