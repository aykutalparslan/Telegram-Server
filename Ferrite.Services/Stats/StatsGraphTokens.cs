// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Security.Cryptography;
using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Stats;

/// <summary>
/// Issues and resolves the tokens `statsGraphAsync` hands out.
///
/// A token is DETERMINISTIC in the graph it names, so re-opening a channel's
/// statistics rewrites the same row instead of accumulating one per request, and
/// the stored set stays bounded by the number of graphs rather than by traffic.
/// It is still OPAQUE: it is a hash, so a client cannot mint one for a channel it
/// cannot already read, and a token Ferrite never issued has no row and is
/// REFUSED rather than answered with an empty graph.
///
/// THE DARK FLAG IS PART OF THE TOKEN'S IDENTITY. `stats.loadAsyncGraph` carries
/// no `dark` flag of its own — only the three statistics methods do — so the
/// palette a graph is rendered in has to be decided when the token is issued and
/// remembered with it. A light and a dark token therefore name the same data and
/// are still two different tokens.
///
/// A token also EXPIRES. Statistics are a snapshot of a moment, and honouring a
/// week-old token would answer today's numbers to a question asked about last
/// week's chart.
/// </summary>
public sealed class StatsGraphTokens
{
    private readonly IStatisticsRepository _statisticsRepository;

    /// <summary>
    /// How long an issued token stays loadable. A client opens a statistics
    /// screen and loads its graphs within seconds, so this only ever refuses a
    /// token that was kept far longer than the answer it belonged to.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private const string Salt = "ferrite-stats-graph";

    private readonly IUnitOfWork _unitOfWork;

    public StatsGraphTokens(IUnitOfWork unitOfWork, IStatisticsRepository statisticsRepository)
    {
        _statisticsRepository = statisticsRepository;

        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Records the server's promise to serve one graph, and answers the token
    /// that redeems it.
    /// </summary>
    public string Issue(long channelId, int messageId, StatsGraphKind kind,
        bool dark, int date)
    {
        string token = Token(channelId, messageId, kind, dark);
        var builder = StatsGraphToken.Builder()
            .Token(Encoding.UTF8.GetBytes(token))
            .ChannelId(channelId)
            .MsgId(messageId)
            .Graph((int)kind)
            .Date(date);
        if (dark)
        {
            builder = builder.Dark(true);
        }
        using TLStatsGraphToken row = builder.Build();
        _statisticsRepository.PutGraphToken(row);
        return token;
    }

    /// <summary>
    /// Issues one token per graph and wraps each in the `statsGraphAsync`
    /// placeholder the statistics answer carries. The result OWNS pooled memory
    /// for every placeholder and must be disposed after the answer is built,
    /// not before: a builder retains its input spans until <c>Build()</c>.
    /// </summary>
    public StatsGraphSet IssueAll(long channelId, int messageId,
        IReadOnlyList<StatsGraphKind> kinds, bool dark, int date)
    {
        var graphs = new Dictionary<StatsGraphKind, TLStatsGraph>(kinds.Count);
        var set = new StatsGraphSet(graphs);
        try
        {
            foreach (StatsGraphKind kind in kinds)
            {
                string token = Issue(channelId, messageId, kind, dark, date);
                graphs[kind] = StatsGraphAsync.Builder()
                    .Token(Encoding.UTF8.GetBytes(token))
                    .Build();
            }
            return set;
        }
        catch
        {
            set.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The graph a token names, or null when Ferrite never issued it or it has
    /// outlived <see cref="Lifetime"/>.
    /// </summary>
    public async Task<ResolvedGraphToken?> ResolveAsync(string token, int now)
    {
        using TLStatsGraphToken? stored = await _statisticsRepository
            .GetGraphTokenAsync(token);
        if (stored == null)
        {
            return null;
        }

        var view = stored.Value.AsStatsGraphToken();
        if (now - view.Date > (int)Lifetime.TotalSeconds)
        {
            return null;
        }
        return new ResolvedGraphToken(view.ChannelId, view.MsgId,
            (StatsGraphKind)view.Graph, view.Dark);
    }

    private static string Token(long channelId, int messageId, StatsGraphKind kind,
        bool dark)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{Salt}:{channelId}:{messageId}:{(int)kind}:{(dark ? 1 : 0)}"));
        return Convert.ToHexStringLower(digest.AsSpan(0, 12));
    }
}

/// <summary>What one issued token redeems.</summary>
public readonly record struct ResolvedGraphToken(long ChannelId, int MessageId,
    StatsGraphKind Kind, bool Dark);

/// <summary>
/// The `statsGraphAsync` placeholders of one statistics answer, owning their
/// pooled memory until the answer they were built into is finished.
/// </summary>
public sealed class StatsGraphSet : IDisposable
{
    private readonly Dictionary<StatsGraphKind, TLStatsGraph> _graphs;

    internal StatsGraphSet(Dictionary<StatsGraphKind, TLStatsGraph> graphs)
    {
        _graphs = graphs;
    }

    public ReadOnlySpan<byte> this[StatsGraphKind kind] => _graphs[kind].AsSpan();

    public void Dispose()
    {
        foreach (TLStatsGraph graph in _graphs.Values)
        {
            graph.Dispose();
        }
        _graphs.Clear();
    }
}
