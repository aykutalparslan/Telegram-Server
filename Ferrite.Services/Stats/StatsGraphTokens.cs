// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Security.Cryptography;
using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Stats;

public sealed class StatsGraphTokens
{
    private readonly IStatisticsRepository _statisticsRepository;

    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private const string Salt = "ferrite-stats-graph";

    private readonly IUnitOfWork _unitOfWork;

    public StatsGraphTokens(IUnitOfWork unitOfWork, IStatisticsRepository statisticsRepository)
    {
        _statisticsRepository = statisticsRepository;

        _unitOfWork = unitOfWork;
    }

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

public readonly record struct ResolvedGraphToken(long ChannelId, int MessageId,
    StatsGraphKind Kind, bool Dark);

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
