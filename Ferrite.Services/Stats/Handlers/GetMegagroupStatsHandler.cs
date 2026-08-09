// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Stats;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.stats;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.StatsMethods;

/// <summary>
/// A supergroup's statistics.
///
/// The three "top" lists are the ones that make a supergroup's statistics
/// different from a broadcast's, and each comes from a row Ferrite already keeps
/// for another purpose: top posters from the shared message box, top
/// administrators from the append-only administrative ledger, and top inviters
/// from the `inviter_id` every membership row records.
///
/// Pinned TDLib DROPS a top-list row whose user it has never seen
/// (`convert_megagroup_stats`, `StatisticsManager.cpp:93-105` filters invalid
/// ids, and `get_user_id_object` needs the user), so every user any list names
/// travels in `users`.
/// </summary>
public sealed class GetMegagroupStatsHandler : StatsHandlerBase
{
    /// <summary>
    /// How many rows each top list carries. Telegram's own supergroup statistics
    /// screen shows a short leaderboard rather than the whole membership.
    /// </summary>
    private const int TopCount = 10;

    private static readonly StatsGraphKind[] Graphs =
    [
        StatsGraphKind.GroupGrowth,
        StatsGraphKind.GroupMembers,
        StatsGraphKind.GroupNewMembersBySource,
        StatsGraphKind.GroupLanguages,
        StatsGraphKind.GroupMessages,
        StatsGraphKind.GroupActions,
        StatsGraphKind.GroupTopHours,
        StatsGraphKind.GroupWeekdays,
    ];

    public GetMegagroupStatsHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IAuthorizationRepository authorizationRepository, IChannelAdminRepository channelAdminRepository, IChatRepository chatRepository, IUserRepository userRepository,
        StatisticsStore statistics, StatsGraphTokens tokens, ILogger log)
        : base(unitOfWork, chatParticipantsRepository, authorizationRepository, channelAdminRepository, chatRepository, userRepository, statistics, tokens, log)
    {
    }

    [TLFunction(Constructors.baseLayer_GetMegagroupStats)]
    public async Task<TLMegagroupStats> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetMegagroupStats)q;
        bool dark = request.Dark;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());

        StatsAccess access = await AuthorizeAsync(authKeyId, channelId);
        if (access.Error != null)
        {
            return Error(access.Error);
        }
        if (!access.Megagroup)
        {
            return Error("MEGAGROUP_REQUIRED");
        }

        ChannelStatsSnapshot snapshot = await _statistics.LoadAsync(access.ChannelId);
        int now = UnixNow();
        StatsCounters.Period period = StatsCounters.CurrentPeriod(now);
        using StatsGraphSet graphs = _tokens.IssueAll(access.ChannelId, 0, Graphs,
            dark, now);
        await _unitOfWork.SaveAsync();

        IReadOnlyList<(long UserId, int Messages, int AverageChars)> topPosters =
            StatsCounters.TopPosters(snapshot, TopCount);
        IReadOnlyList<(long UserId, int Deleted, int Kicked, int Banned)> topAdmins =
            StatsCounters.TopAdmins(snapshot, TopCount);
        IReadOnlyList<(long UserId, int Invitations)> topInviters =
            StatsCounters.TopInviters(snapshot, TopCount);

        using TLStatsDateRangeDays range = StatsDateRangeDays.Builder()
            .MinDate(period.MinDate)
            .MaxDate(period.MaxDate)
            .Build();
        using TLStatsAbsValueAndPrev members =
            AbsValue(StatsCounters.Members(snapshot, period));
        using TLStatsAbsValueAndPrev messages =
            AbsValue(StatsCounters.Messages(snapshot, period));
        using TLStatsAbsValueAndPrev viewers =
            AbsValue(StatsCounters.Viewers(snapshot, period));
        using TLStatsAbsValueAndPrev posters =
            AbsValue(StatsCounters.Posters(snapshot, period));

        var posterVector = new Vector();
        foreach ((long userId, int count, int averageChars) in topPosters)
        {
            using TLStatsGroupTopPoster row = StatsGroupTopPoster.Builder()
                .UserId(userId)
                .Messages(count)
                .AvgChars(averageChars)
                .Build();
            posterVector.AppendTLObject(row.AsSpan());
        }
        var adminVector = new Vector();
        foreach ((long userId, int deleted, int kicked, int banned) in topAdmins)
        {
            using TLStatsGroupTopAdmin row = StatsGroupTopAdmin.Builder()
                .UserId(userId)
                .Deleted(deleted)
                .Kicked(kicked)
                .Banned(banned)
                .Build();
            adminVector.AppendTLObject(row.AsSpan());
        }
        var inviterVector = new Vector();
        foreach ((long userId, int invitations) in topInviters)
        {
            using TLStatsGroupTopInviter row = StatsGroupTopInviter.Builder()
                .UserId(userId)
                .Invitations(invitations)
                .Build();
            inviterVector.AppendTLObject(row.AsSpan());
        }

        var userVector = new Vector();
        AppendUsers(ref userVector, topPosters.Select(x => x.UserId)
            .Concat(topAdmins.Select(x => x.UserId))
            .Concat(topInviters.Select(x => x.UserId)));

        _log.Debug($"📊 GetMegagroupStats user:{access.UserId} " +
                   $"channel:{access.ChannelId} messages:{snapshot.Messages.Count} " +
                   $"members:{snapshot.Members.Count}");
        return MegagroupStats.Builder()
            .Period(range.AsSpan())
            .Members(members.AsSpan())
            .Messages(messages.AsSpan())
            .Viewers(viewers.AsSpan())
            .Posters(posters.AsSpan())
            .GrowthGraph(graphs[StatsGraphKind.GroupGrowth])
            .MembersGraph(graphs[StatsGraphKind.GroupMembers])
            .NewMembersBySourceGraph(graphs[StatsGraphKind.GroupNewMembersBySource])
            .LanguagesGraph(graphs[StatsGraphKind.GroupLanguages])
            .MessagesGraph(graphs[StatsGraphKind.GroupMessages])
            .ActionsGraph(graphs[StatsGraphKind.GroupActions])
            .TopHoursGraph(graphs[StatsGraphKind.GroupTopHours])
            .WeekdaysGraph(graphs[StatsGraphKind.GroupWeekdays])
            .TopPosters(posterVector)
            .TopAdmins(adminVector)
            .TopInviters(inviterVector)
            .Users(userVector)
            .Build();
    }

    private static TLStatsAbsValueAndPrev AbsValue(StatsAbsValue value) =>
        StatsAbsValueAndPrev.Builder()
            .Current(value.Current)
            .Previous(value.Previous)
            .Build();

    private static TLMegagroupStats Error(string message) =>
        (TLMegagroupStats)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
