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

public sealed class GetBroadcastStatsHandler : StatsHandlerBase
{
    private const int RecentPosts = 10;

    private static readonly StatsGraphKind[] Graphs =
    [
        StatsGraphKind.ChannelGrowth,
        StatsGraphKind.ChannelFollowers,
        StatsGraphKind.ChannelMute,
        StatsGraphKind.ChannelTopHours,
        StatsGraphKind.ChannelInteractions,
        StatsGraphKind.ChannelInstantViewInteractions,
        StatsGraphKind.ChannelViewsBySource,
        StatsGraphKind.ChannelNewFollowersBySource,
        StatsGraphKind.ChannelLanguages,
        StatsGraphKind.ChannelReactionsByEmotion,
        StatsGraphKind.ChannelStoryInteractions,
        StatsGraphKind.ChannelStoryReactionsByEmotion,
    ];

    public GetBroadcastStatsHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IAuthorizationRepository authorizationRepository, IChannelAdminRepository channelAdminRepository, IChatRepository chatRepository, UserSerializer userSerializer,
        StatisticsStore statistics, StatsGraphTokens tokens, ILogger log)
        : base(unitOfWork, chatParticipantsRepository, authorizationRepository, channelAdminRepository, chatRepository, userSerializer, statistics, tokens, log)
    {
    }

    [TLFunction(Constructors.baseLayer_GetBroadcastStats)]
    public async Task<TLBroadcastStats> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetBroadcastStats)q;
        bool dark = request.Dark;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());

        StatsAccess access = await AuthorizeAsync(authKeyId, channelId);
        if (access.Error != null)
        {
            return Error(access.Error);
        }
        if (access.Megagroup)
        {
            return Error("BROADCAST_REQUIRED");
        }

        ChannelStatsSnapshot snapshot = await _statistics.LoadAsync(access.ChannelId);
        int now = UnixNow();
        StatsCounters.Period period = StatsCounters.CurrentPeriod(now);
        using StatsGraphSet graphs = _tokens.IssueAll(access.ChannelId, 0, Graphs,
            dark, now);
        await _unitOfWork.SaveAsync();

        using TLStatsDateRangeDays range = StatsDateRangeDays.Builder()
            .MinDate(period.MinDate)
            .MaxDate(period.MaxDate)
            .Build();
        using TLStatsAbsValueAndPrev followers =
            AbsValue(StatsCounters.Members(snapshot, period));
        using TLStatsAbsValueAndPrev viewsPerPost = AbsValue(
            StatsCounters.PerPost(snapshot, period, StatsCounters.Views));
        using TLStatsAbsValueAndPrev sharesPerPost = AbsValue(
            StatsCounters.PerPost(snapshot, period, StatsCounters.Forwards));
        using TLStatsAbsValueAndPrev reactionsPerPost = AbsValue(
            StatsCounters.PerPost(snapshot, period, StatsCounters.Reactions));
        using TLStatsAbsValueAndPrev noStories = AbsValue(default);
        using TLStatsPercentValue notifications = StatsPercentValue.Builder()
            .Part(0)
            .Total(0)
            .Build();

        var recent = new Vector();
        foreach (StatsPostInteractions post in
                 StatsCounters.RecentPosts(snapshot, RecentPosts))
        {
            using TLPostInteractionCounters counters = PostInteractionCountersMessage
                .Builder()
                .MsgId(post.MessageId)
                .Views(post.Views)
                .Forwards(post.Forwards)
                .Reactions(post.Reactions)
                .Build();
            recent.AppendTLObject(counters.AsSpan());
        }

        _log.Debug($"📊 GetBroadcastStats user:{access.UserId} " +
                   $"channel:{access.ChannelId} posts:{snapshot.Messages.Count} " +
                   $"followers:{snapshot.Members.Count}");
        return BroadcastStats.Builder()
            .Period(range.AsSpan())
            .Followers(followers.AsSpan())
            .ViewsPerPost(viewsPerPost.AsSpan())
            .SharesPerPost(sharesPerPost.AsSpan())
            .ReactionsPerPost(reactionsPerPost.AsSpan())
            .ViewsPerStory(noStories.AsSpan())
            .SharesPerStory(noStories.AsSpan())
            .ReactionsPerStory(noStories.AsSpan())
            .EnabledNotifications(notifications.AsSpan())
            .GrowthGraph(graphs[StatsGraphKind.ChannelGrowth])
            .FollowersGraph(graphs[StatsGraphKind.ChannelFollowers])
            .MuteGraph(graphs[StatsGraphKind.ChannelMute])
            .TopHoursGraph(graphs[StatsGraphKind.ChannelTopHours])
            .InteractionsGraph(graphs[StatsGraphKind.ChannelInteractions])
            .IvInteractionsGraph(graphs[StatsGraphKind.ChannelInstantViewInteractions])
            .ViewsBySourceGraph(graphs[StatsGraphKind.ChannelViewsBySource])
            .NewFollowersBySourceGraph(
                graphs[StatsGraphKind.ChannelNewFollowersBySource])
            .LanguagesGraph(graphs[StatsGraphKind.ChannelLanguages])
            .ReactionsByEmotionGraph(graphs[StatsGraphKind.ChannelReactionsByEmotion])
            .StoryInteractionsGraph(graphs[StatsGraphKind.ChannelStoryInteractions])
            .StoryReactionsByEmotionGraph(
                graphs[StatsGraphKind.ChannelStoryReactionsByEmotion])
            .RecentPostsInteractions(recent)
            .Build();
    }

    private static TLStatsAbsValueAndPrev AbsValue(StatsAbsValue value) =>
        StatsAbsValueAndPrev.Builder()
            .Current(value.Current)
            .Previous(value.Previous)
            .Build();

    private static TLBroadcastStats Error(string message) =>
        (TLBroadcastStats)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
