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

public sealed class LoadAsyncGraphHandler : StatsHandlerBase
{
    public LoadAsyncGraphHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IAuthorizationRepository authorizationRepository, IChannelAdminRepository channelAdminRepository, IChatRepository chatRepository, UserSerializer userSerializer, StatisticsStore statistics,
        StatsGraphTokens tokens, ILogger log)
        : base(unitOfWork, chatParticipantsRepository, authorizationRepository, channelAdminRepository, chatRepository, userSerializer, statistics, tokens, log)
    {
    }

    [TLFunction(Constructors.baseLayer_LoadAsyncGraph)]
    public async Task<TLStatsGraph> Handle(long authKeyId, TLBytes q)
    {
        var request = (LoadAsyncGraph)q;
        string token = Encoding.UTF8.GetString(request.Token);
        bool zoomed = request.Flags[0];
        long x = request.X;

        int now = UnixNow();
        ResolvedGraphToken? resolved = await _tokens.ResolveAsync(token, now);
        if (resolved == null)
        {
            return Error("GRAPH_EXPIRED_RELOAD");
        }

        ResolvedGraphToken graph = resolved.Value;
        StatsAccess access = await AuthorizeAsync(authKeyId, graph.ChannelId);
        if (access.Error != null)
        {
            return Error(access.Error);
        }

        ChannelStatsSnapshot snapshot = await _statistics.LoadAsync(graph.ChannelId);

        string? json = zoomed
            ? StatsGraphs.BuildZoom(graph.Kind, snapshot, graph.MessageId,
                (int)(x / 1000), graph.Dark)
            : StatsGraphs.Build(graph.Kind, snapshot, graph.MessageId, graph.Dark);
        if (json == null)
        {
            return GraphError("NOT_ENOUGH_DATA");
        }

        _log.Debug($"📊 LoadAsyncGraph user:{access.UserId} " +
                   $"channel:{graph.ChannelId} graph:{graph.Kind}" +
                   (zoomed ? $" zoom:{x}" : string.Empty));
        return Graph(json, null);
    }

    private static TLStatsGraph Error(string message) =>
        (TLStatsGraph)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
