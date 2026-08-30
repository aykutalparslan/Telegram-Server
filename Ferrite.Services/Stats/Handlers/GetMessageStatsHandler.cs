// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Stats;
using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.stats;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.StatsMethods;

public sealed class GetMessageStatsHandler : StatsHandlerBase
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;

    public GetMessageStatsHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IAuthorizationRepository authorizationRepository, IChannelAdminRepository channelAdminRepository, IChannelMessagesRepository channelMessagesRepository, IChatRepository chatRepository, UserSerializer userSerializer,
        StatisticsStore statistics, StatsGraphTokens tokens, ILogger log)
        : base(unitOfWork, chatParticipantsRepository, authorizationRepository, channelAdminRepository, chatRepository, userSerializer, statistics, tokens, log)
    {
        _channelMessagesRepository = channelMessagesRepository;

    }

    [TLFunction(Constructors.baseLayer_GetMessageStats)]
    public async Task<TLMessageStats> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetMessageStats)q;
        bool dark = request.Dark;
        int messageId = request.MsgId;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());

        StatsAccess access = await AuthorizeAsync(authKeyId, channelId);
        if (access.Error != null)
        {
            return Error(access.Error);
        }

        using (TLSavedMessage? stored = await _channelMessagesRepository
                   .GetMessageAsync(access.ChannelId, messageId))
        {
            if (stored == null)
            {
                return Error("MSG_ID_INVALID");
            }
        }

        ChannelStatsSnapshot snapshot = await _statistics.LoadAsync(access.ChannelId);
        string? views = StatsGraphs.Build(StatsGraphKind.MessageViews, snapshot,
            messageId, dark);
        string? reactions = StatsGraphs.Build(
            StatsGraphKind.MessageReactionsByEmotion, snapshot, messageId, dark);

        string? zoom = views == null
            ? null
            : _tokens.Issue(access.ChannelId, messageId, StatsGraphKind.MessageViews,
                dark, UnixNow());
        await _unitOfWork.SaveAsync();

        _log.Debug($"📊 GetMessageStats user:{access.UserId} " +
                   $"channel:{access.ChannelId} message:{messageId}");
        using TLStatsGraph viewsGraph = Graph(views, zoom);
        using TLStatsGraph reactionsGraph = Graph(reactions, null);
        return MessageStats.Builder()
            .ViewsGraph(viewsGraph.AsSpan())
            .ReactionsByEmotionGraph(reactionsGraph.AsSpan())
            .Build();
    }

    private static TLMessageStats Error(string message) =>
        (TLMessageStats)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
