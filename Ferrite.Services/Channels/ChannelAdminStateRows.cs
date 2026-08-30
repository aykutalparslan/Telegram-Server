// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Services.Stats;
using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Channels;

public static class ChannelAdminStateRows
{
    public static TLChannelAdminState Empty(long channelId, int date) =>
        ChannelAdminState.Builder()
            .CanViewStats(true)
            .ChannelId(channelId)
            .SlowmodeSeconds(0)
            .BoostsUnrestrict(0)
            .StatsDc(StatisticsStore.StatsDcId)
            .Date(date)
            .Build();

    public static TLChannelAdminState WithFlags(ChannelAdminState source,
        Flags flags, int date) =>
        Rebuild(source, flags, source.Location, source.LinkedChatId,
            source.MainTab, source.SlowmodeSeconds, source.BoostsUnrestrict,
            source.StatsDc, date);

    public static TLChannelAdminState WithSlowModeSeconds(
        ChannelAdminState source, int slowmodeSeconds, int date) =>
        Rebuild(source, source.Flags, source.Location, source.LinkedChatId,
            source.MainTab, slowmodeSeconds, source.BoostsUnrestrict,
            source.StatsDc, date);

    public static TLChannelAdminState WithBoostsUnrestrict(
        ChannelAdminState source, int boostsUnrestrict, int date) =>
        Rebuild(source, source.Flags, source.Location, source.LinkedChatId,
            source.MainTab, source.SlowmodeSeconds, boostsUnrestrict,
            source.StatsDc, date);

    public static TLChannelAdminState WithStatistics(ChannelAdminState source,
        bool canViewStats, int statsDc, int date)
    {
        Flags flags = source.Flags;
        flags[3] = canViewStats;
        return Rebuild(source, flags, source.Location, source.LinkedChatId,
            source.MainTab, source.SlowmodeSeconds, source.BoostsUnrestrict,
            statsDc, date);
    }

    public static TLChannelAdminState WithLinkedChatId(ChannelAdminState source,
        long linkedChatId, int date)
    {
        Flags flags = source.Flags;
        flags[5] = linkedChatId != 0;
        return Rebuild(source, flags, source.Location, linkedChatId,
            source.MainTab, source.SlowmodeSeconds, source.BoostsUnrestrict,
            source.StatsDc, date);
    }

    public static TLChannelAdminState WithLocation(ChannelAdminState source,
        ReadOnlySpan<byte> location, int date)
    {
        Flags flags = source.Flags;
        flags[4] = location.Length > 0;
        return Rebuild(source, flags, location, source.LinkedChatId,
            source.MainTab, source.SlowmodeSeconds, source.BoostsUnrestrict,
            source.StatsDc, date);
    }

    public static TLChannelAdminState WithMainTab(ChannelAdminState source,
        ReadOnlySpan<byte> mainTab, int date)
    {
        Flags flags = source.Flags;
        flags[6] = mainTab.Length > 0;
        return Rebuild(source, flags, source.Location, source.LinkedChatId,
            mainTab, source.SlowmodeSeconds, source.BoostsUnrestrict,
            source.StatsDc, date);
    }

    private static TLChannelAdminState Rebuild(ChannelAdminState source,
        Flags flags, ReadOnlySpan<byte> location, long linkedChatId,
        ReadOnlySpan<byte> mainTab, int slowmodeSeconds, int boostsUnrestrict,
        int statsDc, int date) =>
        new ChannelAdminState(flags, flags[0], flags[1], flags[2], flags[3],
            location, linkedChatId, mainTab, source.ChannelId, slowmodeSeconds,
            boostsUnrestrict, statsDc, date);
}
