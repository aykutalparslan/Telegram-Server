// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class StatisticsRepository : IStatisticsRepository
{
    private readonly IKVStore _publicForwards;
    private readonly IKVStore _graphTokens;

    public StatisticsRepository(IKVStore publicForwards, IKVStore graphTokens)
    {
        _publicForwards = publicForwards;
        _graphTokens = graphTokens;

        publicForwards.SetSchema(new TableDefinition("ferrite", "public_forwards",
            new KeyDefinition("pk",
                new DataColumn { Name = "channel_id", Type = DataType.Long },
                new DataColumn { Name = "msg_id", Type = DataType.Int },
                new DataColumn { Name = "fwd_channel_id", Type = DataType.Long },
                new DataColumn { Name = "fwd_msg_id", Type = DataType.Int })));
        graphTokens.SetSchema(new TableDefinition("ferrite", "stats_graph_tokens",
            new KeyDefinition("pk",
                new DataColumn { Name = "graph_token", Type = DataType.String })));
    }

    public bool PutPublicForward(TLPublicForwardRef row)
    {
        var view = row.AsPublicForwardRef();
        return _publicForwards.Put(row.AsSpan().ToArray(), view.ChannelId,
            view.MsgId, view.FwdChannelId, view.FwdMsgId);
    }

    public async ValueTask<IReadOnlyCollection<TLPublicForwardRef>>
        GetPublicForwardsAsync(long channelId, int msgId)
    {
        var rows = new List<TLPublicForwardRef>();
        await foreach (byte[] bytes in _publicForwards.IterateAsync(channelId, msgId))
        {
            rows.Add(new TLPublicForwardRef(bytes, 0, bytes.Length));
        }
        return rows;
    }

    public bool PutGraphToken(TLStatsGraphToken row) =>
        _graphTokens.Put(row.AsSpan().ToArray(),
            System.Text.Encoding.UTF8.GetString(row.AsStatsGraphToken().Token));

    public async ValueTask<TLStatsGraphToken?> GetGraphTokenAsync(string token)
    {
        byte[]? bytes = await _graphTokens.GetAsync(token);
        return bytes is { Length: > 0 }
            ? new TLStatsGraphToken(bytes, 0, bytes.Length) : null;
    }
}
