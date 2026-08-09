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
/// Redeems a `statsGraphAsync` token for the graph it names.
///
/// This is where every graph in every statistics answer is actually computed:
/// the three statistics methods hand out nothing but tokens, so this method is
/// the only producer of chart data in Ferrite.
///
/// A token Ferrite never issued, or one that has outlived
/// <see cref="StatsGraphTokens.Lifetime"/>, is an ERROR rather than an empty
/// graph. The two are different claims: an empty graph says the period had no
/// activity, while a bad token says the question cannot be answered at all, and
/// collapsing them would hide a client paging with a stale statistics screen.
///
/// The caller's access is re-checked against the channel the token names. A
/// token is opaque and unguessable, but it is not a capability: an administrator
/// who is demoted between opening the screen and loading a graph must stop being
/// able to read it.
/// </summary>
public sealed class LoadAsyncGraphHandler : StatsHandlerBase
{
    public LoadAsyncGraphHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IAuthorizationRepository authorizationRepository, IChannelAdminRepository channelAdminRepository, IChatRepository chatRepository, IUserRepository userRepository, StatisticsStore statistics,
        StatsGraphTokens tokens, ILogger log)
        : base(unitOfWork, chatParticipantsRepository, authorizationRepository, channelAdminRepository, chatRepository, userRepository, statistics, tokens, log)
    {
    }

    [TLFunction(Constructors.baseLayer_LoadAsyncGraph)]
    public async Task<TLStatsGraph> Handle(long authKeyId, TLBytes q)
    {
        var request = (LoadAsyncGraph)q;
        string token = Encoding.UTF8.GetString(request.Token);
        bool zoomed = request.Flags[0];

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

        if (zoomed)
        {
            // `x` drills into one label of a ZOOMABLE graph, and a graph is only
            // zoomable when its answer carried a `zoom_token`. Ferrite issues
            // none, so no client can legitimately arrive here; the documented
            // answer for a subgraph that cannot be produced is statsGraphError
            // rather than a failed request.
            return GraphError("ZOOM_NOT_AVAILABLE");
        }

        ChannelStatsSnapshot snapshot = await _statistics.LoadAsync(graph.ChannelId);
        string json = StatsGraphs.Build(graph.Kind, snapshot, graph.MessageId,
            graph.Dark);

        _log.Debug($"📊 LoadAsyncGraph user:{access.UserId} " +
                   $"channel:{graph.ChannelId} graph:{graph.Kind}");
        using TLDataJSON data = DataJSON.Builder()
            .Data(Encoding.UTF8.GetBytes(json))
            .Build();
        return StatsGraph.Builder().Json(data.AsSpan()).Build();
    }

    /// <summary>
    /// A graph the server understands but cannot produce, reported inside the
    /// answer rather than as a failed request — the shape the client expects
    /// when "not enough data is available".
    /// </summary>
    private static TLStatsGraph GraphError(string message) =>
        StatsGraphError.Builder()
            .Error(Encoding.UTF8.GetBytes(message))
            .Build();

    private static TLStatsGraph Error(string message) =>
        (TLStatsGraph)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
