// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Stats;

/// <summary>
/// Builds one graph's chart JSON from a snapshot of the rows Ferrite stores.
///
/// A graph Ferrite has NO SOURCE for answers with its columns declared and no
/// points, and there are five of those. They are listed here rather than left
/// implicit, because "empty" is a claim about Ferrite and not about the channel:
///
///   - MUTE and ENABLED NOTIFICATIONS: notification settings are keyed by AUTH
///     KEY, not by user, so a channel's members cannot be asked whether they
///     muted it.
///   - VIEWS BY SOURCE and NEW FOLLOWERS BY SOURCE: Ferrite attributes neither a
///     view nor a join to a referral source.
///   - LANGUAGES: no per-member language is recorded against a channel.
///   - INSTANT VIEW INTERACTIONS: Ferrite serves no instant-view pages.
///   - STORY INTERACTIONS and STORY REACTIONS: every `stories.*` method is
///     permanently disabled, so no story exists to interact with.
///
/// The MEMBERSHIP graphs are built from CURRENT members' join dates alone, never
/// from the administrative ledger's join/leave events. Those two sources cannot
/// be mixed: a member who left and rejoined appears once in the ledger's leave
/// events and once in the current rows, so summing both drives the running total
/// below the real count. Building from current membership keeps one strong
/// property instead — the final cumulative value IS the channel's member count.
/// Churn is therefore not visible, which is a limit of what Ferrite retains.
/// </summary>
public static class StatsGraphs
{
    private const int SecondsPerDay = 24 * 60 * 60;

    public static string Build(StatsGraphKind kind, ChannelStatsSnapshot snapshot,
        int messageId, bool dark) => kind switch
    {
        StatsGraphKind.ChannelGrowth or StatsGraphKind.GroupGrowth =>
            CumulativeMembers(snapshot, dark),
        StatsGraphKind.ChannelFollowers or StatsGraphKind.GroupMembers =>
            NewMembers(snapshot, dark),
        StatsGraphKind.ChannelTopHours => ByHour(snapshot.Views.Select(x => x.Date),
            "Views", StatsChart.Blue, dark),
        StatsGraphKind.GroupTopHours => ByHour(snapshot.Messages.Select(x => x.Date),
            "Messages", StatsChart.Blue, dark),
        StatsGraphKind.GroupWeekdays => ByWeekday(snapshot, dark),
        StatsGraphKind.ChannelInteractions => Interactions(snapshot, dark),
        StatsGraphKind.GroupMessages => ByDay(snapshot.Messages.Select(x => x.Date),
            "Messages", StatsChart.Blue, StatsChart.Line, dark),
        StatsGraphKind.GroupActions => Actions(snapshot, dark),
        StatsGraphKind.ChannelReactionsByEmotion =>
            ReactionsByEmotion(snapshot.Reactions, dark),
        StatsGraphKind.MessageViews => ByDay(
            snapshot.Views.Where(x => x.MessageId == messageId).Select(x => x.Date),
            "Views", StatsChart.Blue, StatsChart.Line, dark),
        StatsGraphKind.MessageReactionsByEmotion => ReactionsByEmotion(
            snapshot.Reactions.Where(x => x.MessageId == messageId).ToList(), dark),
        StatsGraphKind.ChannelMute => NoData("Muted", dark),
        StatsGraphKind.ChannelInstantViewInteractions => NoData("Views", dark),
        StatsGraphKind.ChannelViewsBySource => NoData("Views", dark),
        StatsGraphKind.ChannelNewFollowersBySource or
            StatsGraphKind.GroupNewMembersBySource => NoData("Followers", dark),
        StatsGraphKind.ChannelLanguages or StatsGraphKind.GroupLanguages =>
            NoData("Members", dark),
        StatsGraphKind.ChannelStoryInteractions => NoData("Views", dark),
        StatsGraphKind.ChannelStoryReactionsByEmotion => NoData("Reactions", dark),
        _ => NoData("Value", dark),
    };

    /// <summary>
    /// A well-formed chart with its column declared and no points, which is what
    /// a graph with no rows behind it answers.
    /// </summary>
    private static string NoData(string name, bool dark) =>
        StatsChart.Build(StatsChart.Line, [],
            [new StatsChartSeries(name, StatsChart.Blue, [])], dark);

    private static string CumulativeMembers(ChannelStatsSnapshot snapshot, bool dark)
    {
        IReadOnlyList<int> days = Days(snapshot.Members.Select(x => x.Date));
        var counts = new long[days.Count];
        foreach (StatsMember member in snapshot.Members)
        {
            // A join contributes to its own day and to every later one.
            for (int i = IndexOf(days, StartOfDay(member.Date)); i < days.Count; i++)
            {
                counts[i]++;
            }
        }
        return StatsChart.Build(StatsChart.Line, days,
            [new StatsChartSeries("Members", StatsChart.Blue, counts)], dark);
    }

    private static string NewMembers(ChannelStatsSnapshot snapshot, bool dark) =>
        ByDay(snapshot.Members.Select(x => x.Date), "New members", StatsChart.Green,
            StatsChart.Line, dark);

    private static string Interactions(ChannelStatsSnapshot snapshot, bool dark)
    {
        IReadOnlyList<int> days = Days(snapshot.Views.Select(x => x.Date)
            .Concat(snapshot.Forwards.Select(x => x.Date)));
        return StatsChart.Build(StatsChart.Line, days,
        [
            new StatsChartSeries("Views", StatsChart.Blue,
                Counts(days, snapshot.Views.Select(x => x.Date))),
            new StatsChartSeries("Shares", StatsChart.Green,
                Counts(days, snapshot.Forwards.Select(x => x.Date))),
        ], dark);
    }

    private static string Actions(ChannelStatsSnapshot snapshot, bool dark)
    {
        IReadOnlyList<int> days = Days(snapshot.AdminActions.Select(x => x.Date));
        return StatsChart.Build(StatsChart.Line, days,
        [
            new StatsChartSeries("Deletions", StatsChart.Red,
                Counts(days, snapshot.AdminActions
                    .Where(x => x.Kind == StatsAdminActionKind.Deleted)
                    .Select(x => x.Date))),
            new StatsChartSeries("Removals", StatsChart.Orange,
                Counts(days, snapshot.AdminActions
                    .Where(x => x.Kind == StatsAdminActionKind.Kicked)
                    .Select(x => x.Date))),
            new StatsChartSeries("Restrictions", StatsChart.Golden,
                Counts(days, snapshot.AdminActions
                    .Where(x => x.Kind == StatsAdminActionKind.Banned)
                    .Select(x => x.Date))),
        ], dark);
    }

    /// <summary>
    /// One stacked bar column per emoticon, over the days reactions were left.
    /// The emoticons are the ones that actually occur, in descending frequency,
    /// so the chart never declares a column with no reactions in it.
    /// </summary>
    private static string ReactionsByEmotion(IReadOnlyList<StatsReaction> reactions,
        bool dark)
    {
        IReadOnlyList<int> days = Days(reactions.Select(x => x.Date));
        List<string> emoticons = reactions
            .GroupBy(x => x.Emoticon, StringComparer.Ordinal)
            .OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => x.Key)
            .ToList();
        if (emoticons.Count == 0)
        {
            return NoData("Reactions", dark);
        }

        var series = new List<StatsChartSeries>(emoticons.Count);
        for (int i = 0; i < emoticons.Count; i++)
        {
            string emoticon = emoticons[i];
            series.Add(new StatsChartSeries(emoticon, StatsChart.ColorAt(i),
                Counts(days, reactions
                    .Where(x => string.Equals(x.Emoticon, emoticon, StringComparison.Ordinal))
                    .Select(x => x.Date))));
        }
        return StatsChart.Build(StatsChart.Bar, days, series, dark, stacked: true);
    }

    private static string ByDay(IEnumerable<int> dates, string name, string color,
        string type, bool dark)
    {
        List<int> materialized = dates.ToList();
        IReadOnlyList<int> days = Days(materialized);
        return StatsChart.Build(type, days,
            [new StatsChartSeries(name, color, Counts(days, materialized))], dark);
    }

    /// <summary>
    /// A distribution over the 24 hours of the day. The x axis is still real
    /// time — the first 24 hours of the epoch — because the chart format has no
    /// other axis, and the client labels it by hour.
    /// </summary>
    private static string ByHour(IEnumerable<int> dates, string name, string color,
        bool dark)
    {
        var counts = new long[24];
        bool any = false;
        foreach (int date in dates)
        {
            counts[DateTimeOffset.FromUnixTimeSeconds(date).UtcDateTime.Hour]++;
            any = true;
        }
        if (!any)
        {
            return NoData(name, dark);
        }

        var hours = new int[24];
        for (int hour = 0; hour < 24; hour++)
        {
            hours[hour] = hour * 60 * 60;
        }
        return StatsChart.Build(StatsChart.Bar, hours,
            [new StatsChartSeries(name, color, counts)], dark, stacked: true);
    }

    /// <summary>
    /// A distribution over the seven weekdays, starting from a Monday so the
    /// client's own weekday labels line up with the buckets.
    /// </summary>
    private static string ByWeekday(ChannelStatsSnapshot snapshot, bool dark)
    {
        if (snapshot.Messages.Count == 0)
        {
            return NoData("Messages", dark);
        }

        var counts = new long[7];
        foreach (StatsMessage message in snapshot.Messages)
        {
            counts[(int)DateTimeOffset.FromUnixTimeSeconds(message.Date)
                .UtcDateTime.DayOfWeek]++;
        }

        // The buckets are indexed by DayOfWeek, which counts from SUNDAY, so the
        // x axis starts on 1970-01-04 — the first Sunday of the epoch, three days
        // after the Thursday it begins on.
        var days = new int[7];
        for (int i = 0; i < 7; i++)
        {
            days[i] = (3 + i) * SecondsPerDay;
        }
        return StatsChart.Build(StatsChart.Bar, days,
        [
            new StatsChartSeries("Messages", StatsChart.Blue, counts),
        ], dark, stacked: true);
    }

    /// <summary>
    /// The ordered, gap-free run of days the given timestamps fall in. Gaps are
    /// filled so a chart never draws a straight line across a silent week.
    /// </summary>
    private static IReadOnlyList<int> Days(IEnumerable<int> dates)
    {
        int first = int.MaxValue;
        int last = int.MinValue;
        foreach (int date in dates)
        {
            int day = StartOfDay(date);
            first = Math.Min(first, day);
            last = Math.Max(last, day);
        }
        if (first > last)
        {
            return [];
        }

        var days = new List<int>((last - first) / SecondsPerDay + 1);
        for (int day = first; day <= last; day += SecondsPerDay)
        {
            days.Add(day);
        }
        return days;
    }

    private static long[] Counts(IReadOnlyList<int> days, IEnumerable<int> dates)
    {
        var counts = new long[days.Count];
        foreach (int date in dates)
        {
            int index = IndexOf(days, StartOfDay(date));
            if (index < days.Count)
            {
                counts[index]++;
            }
        }
        return counts;
    }

    // The days are a gap-free ascending run, so the index is arithmetic.
    private static int IndexOf(IReadOnlyList<int> days, int day) =>
        days.Count == 0 ? 0 : Math.Clamp((day - days[0]) / SecondsPerDay, 0, days.Count);

    private static int StartOfDay(int date) => date - date % SecondsPerDay;
}
