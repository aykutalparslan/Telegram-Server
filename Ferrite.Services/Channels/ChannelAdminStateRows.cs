// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Services.Stats;
using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Channels;

/// <summary>
/// Rebuilds the durable `dto.channelAdminState` row. Three of its fields
/// (`location`, `linked_chat_id`, `main_tab`) are value-gated, and a generated
/// builder can SET the gating flag but never clear it, so every mutation goes
/// through the value constructor with an explicitly adjusted flag word. Every
/// field the stored row carries is re-emitted; only the one a mutation names may
/// appear or disappear.
///
/// The row holds only what `channel#fe685355` has no place for. The compact
/// channel row stays authoritative for `signatures`, `join_to_send`, `color`,
/// `emoji_status` and the rest it already carries, so nothing here shadows it.
/// </summary>
public static class ChannelAdminStateRows
{
    /// <summary>
    /// The row a channel with no stored administration state behaves as. Every
    /// flag is clear and every counter zero EXCEPT the statistics pair, because
    /// Ferrite can serve statistics for any channel it stores: they are derived
    /// from membership, messages, view receipts, reactions and the
    /// administrative ledger, all of which exist from the moment a channel does.
    /// Callers mutate this instead of branching on absence.
    ///
    /// The pair moves together on purpose. `can_view_stats` alone is actively
    /// harmful — see <see cref="WithStatistics"/>.
    /// </summary>
    public static TLChannelAdminState Empty(long channelId, int date) =>
        ChannelAdminState.Builder()
            .CanViewStats(true)
            .ChannelId(channelId)
            .SlowmodeSeconds(0)
            .BoostsUnrestrict(0)
            .StatsDc(StatisticsStore.StatsDcId)
            .Date(date)
            .Build();

    /// <summary>
    /// Rebuilds with a new flag word, which covers every bare `flags.N?true`
    /// property: `antispam`, `participants_hidden`, `hidden_prehistory` and
    /// `can_view_stats`.
    /// </summary>
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

    /// <summary>
    /// Sets or clears the statistics pair. `can_view_stats` alone is actively
    /// harmful: pinned TDLib CLEARS the flag and logs an error when `stats_dc` is
    /// absent or out of range (`ChatManager.cpp:5769-5775`), so the two move
    /// together.
    /// </summary>
    public static TLChannelAdminState WithStatistics(ChannelAdminState source,
        bool canViewStats, int statsDc, int date)
    {
        Flags flags = source.Flags;
        flags[3] = canViewStats;
        return Rebuild(source, flags, source.Location, source.LinkedChatId,
            source.MainTab, source.SlowmodeSeconds, source.BoostsUnrestrict,
            statsDc, date);
    }

    /// <summary>
    /// Sets or clears the discussion linkage; a zero id unlinks.
    /// </summary>
    public static TLChannelAdminState WithLinkedChatId(ChannelAdminState source,
        long linkedChatId, int date)
    {
        Flags flags = source.Flags;
        flags[5] = linkedChatId != 0;
        return Rebuild(source, flags, source.Location, linkedChatId,
            source.MainTab, source.SlowmodeSeconds, source.BoostsUnrestrict,
            source.StatsDc, date);
    }

    /// <summary>
    /// Sets or clears the megagroup's geo location; an empty value unsets it.
    /// </summary>
    public static TLChannelAdminState WithLocation(ChannelAdminState source,
        ReadOnlySpan<byte> location, int date)
    {
        Flags flags = source.Flags;
        flags[4] = location.Length > 0;
        return Rebuild(source, flags, location, source.LinkedChatId,
            source.MainTab, source.SlowmodeSeconds, source.BoostsUnrestrict,
            source.StatsDc, date);
    }

    /// <summary>
    /// Sets or clears the profile tab surfaced in `channelFull.main_tab`.
    /// </summary>
    public static TLChannelAdminState WithMainTab(ChannelAdminState source,
        ReadOnlySpan<byte> mainTab, int date)
    {
        Flags flags = source.Flags;
        flags[6] = mainTab.Length > 0;
        return Rebuild(source, flags, source.Location, source.LinkedChatId,
            mainTab, source.SlowmodeSeconds, source.BoostsUnrestrict,
            source.StatsDc, date);
    }

    // The one place where the full field list of `dto.channelAdminState` is
    // enumerated, so a schema addition is a compile error rather than a field
    // silently dropped from every rewritten row.
    private static TLChannelAdminState Rebuild(ChannelAdminState source,
        Flags flags, ReadOnlySpan<byte> location, long linkedChatId,
        ReadOnlySpan<byte> mainTab, int slowmodeSeconds, int boostsUnrestrict,
        int statsDc, int date) =>
        new ChannelAdminState(flags, flags[0], flags[1], flags[2], flags[3],
            location, linkedChatId, mainTab, source.ChannelId, slowmodeSeconds,
            boostsUnrestrict, statsDc, date);
}
