// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Stats;

public readonly record struct StatsAbsValue(double Current, double Previous);

public readonly record struct StatsPostInteractions(int MessageId, int Views,
    int Forwards, int Reactions);

public static class StatsCounters
{
    public const int PeriodDays = 7;
    private const int SecondsPerDay = 24 * 60 * 60;

    public readonly record struct Period(int MinDate, int MaxDate)
    {
        public int PreviousMinDate => MinDate - PeriodDays * SecondsPerDay;

        public bool Contains(int date) => date >= MinDate && date <= MaxDate;

        public bool ContainsPrevious(int date) =>
            date >= PreviousMinDate && date < MinDate;
    }

    public static Period CurrentPeriod(int now) =>
        new(now - PeriodDays * SecondsPerDay, now);

    public static StatsAbsValue Members(ChannelStatsSnapshot snapshot, Period period) =>
        new(snapshot.Members.Count,
            snapshot.Members.Count(x => x.Date < period.MinDate));

    public static StatsAbsValue Messages(ChannelStatsSnapshot snapshot, Period period) =>
        new(snapshot.Messages.Count(x => period.Contains(x.Date)),
            snapshot.Messages.Count(x => period.ContainsPrevious(x.Date)));

    public static StatsAbsValue Viewers(ChannelStatsSnapshot snapshot, Period period) =>
        new(snapshot.Views.Where(x => period.Contains(x.Date))
                .Select(x => x.UserId).Distinct().Count(),
            snapshot.Views.Where(x => period.ContainsPrevious(x.Date))
                .Select(x => x.UserId).Distinct().Count());

    public static StatsAbsValue Posters(ChannelStatsSnapshot snapshot, Period period) =>
        new(snapshot.Messages.Where(x => period.Contains(x.Date))
                .Select(x => x.SenderUserId).Distinct().Count(),
            snapshot.Messages.Where(x => period.ContainsPrevious(x.Date))
                .Select(x => x.SenderUserId).Distinct().Count());

    public static StatsAbsValue PerPost(ChannelStatsSnapshot snapshot, Period period,
        Func<ChannelStatsSnapshot, IEnumerable<(int MessageId, int Date)>> interactions)
    {
        List<(int MessageId, int Date)> all = interactions(snapshot).ToList();
        return new StatsAbsValue(
            Average(snapshot, all, period.Contains),
            Average(snapshot, all, period.ContainsPrevious));
    }

    public static IEnumerable<(int MessageId, int Date)> Views(
        ChannelStatsSnapshot snapshot) =>
        snapshot.Views.Select(x => (x.MessageId, x.Date));

    public static IEnumerable<(int MessageId, int Date)> Forwards(
        ChannelStatsSnapshot snapshot) =>
        snapshot.Forwards.Select(x => (x.MessageId, x.Date));

    public static IEnumerable<(int MessageId, int Date)> Reactions(
        ChannelStatsSnapshot snapshot) =>
        snapshot.Reactions.Select(x => (x.MessageId, x.Date));

    public static IReadOnlyList<StatsPostInteractions> RecentPosts(
        ChannelStatsSnapshot snapshot, int limit) =>
        snapshot.Messages
            .OrderByDescending(x => x.Id)
            .Take(limit)
            .Select(message => new StatsPostInteractions(message.Id,
                snapshot.Views.Count(x => x.MessageId == message.Id),
                snapshot.Forwards.Count(x => x.MessageId == message.Id),
                snapshot.Reactions.Count(x => x.MessageId == message.Id)))
            .ToList();

    public static IReadOnlyList<(long UserId, int Messages, int AverageChars)>
        TopPosters(ChannelStatsSnapshot snapshot, int limit) =>
        snapshot.Messages
            .Where(x => x.SenderUserId > 0)
            .GroupBy(x => x.SenderUserId)
            .Select(group => (UserId: group.Key, Messages: group.Count(),
                AverageChars: (int)group.Average(x => x.Length)))
            .OrderByDescending(x => x.Messages).ThenBy(x => x.UserId)
            .Take(limit)
            .ToList();

    public static IReadOnlyList<(long UserId, int Deleted, int Kicked, int Banned)>
        TopAdmins(ChannelStatsSnapshot snapshot, int limit) =>
        snapshot.AdminActions
            .GroupBy(x => x.UserId)
            .Select(group => (UserId: group.Key,
                Deleted: group.Count(x => x.Kind == StatsAdminActionKind.Deleted),
                Kicked: group.Count(x => x.Kind == StatsAdminActionKind.Kicked),
                Banned: group.Count(x => x.Kind == StatsAdminActionKind.Banned)))
            .OrderByDescending(x => x.Deleted + x.Kicked + x.Banned)
            .ThenBy(x => x.UserId)
            .Take(limit)
            .ToList();

    public static IReadOnlyList<(long UserId, int Invitations)> TopInviters(
        ChannelStatsSnapshot snapshot, int limit) =>
        snapshot.Members
            .Where(x => x.InviterId > 0 && x.InviterId != x.UserId)
            .GroupBy(x => x.InviterId)
            .Select(group => (UserId: group.Key, Invitations: group.Count()))
            .OrderByDescending(x => x.Invitations).ThenBy(x => x.UserId)
            .Take(limit)
            .ToList();

    private static double Average(ChannelStatsSnapshot snapshot,
        IReadOnlyList<(int MessageId, int Date)> interactions,
        Func<int, bool> inPeriod)
    {
        HashSet<int> posts = snapshot.Messages
            .Where(x => inPeriod(x.Date))
            .Select(x => x.Id)
            .ToHashSet();
        if (posts.Count == 0)
        {
            return 0;
        }

        int total = interactions.Count(x => posts.Contains(x.MessageId));
        return (double)total / posts.Count;
    }
}
