// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class ChannelAdminLogRepository : IChannelAdminLogRepository
{
    private readonly IKVStore _events;

    public ChannelAdminLogRepository(IKVStore events)
    {
        _events = events;

        events.SetSchema(new TableDefinition("ferrite", "channel_admin_log_events",
            new KeyDefinition("pk",
                new DataColumn { Name = "channel_id", Type = DataType.Long },
                new DataColumn { Name = "id", Type = DataType.Long })));
    }

    public bool PutEvent(TLAdminLogEvent row)
    {
        var view = row.AsAdminLogEvent();
        return _events.Put(row.AsSpan().ToArray(), view.ChannelId, view.Id);
    }

    public async ValueTask<IReadOnlyCollection<TLAdminLogEvent>> GetEventsAsync(
        long channelId)
    {
        var rows = new List<TLAdminLogEvent>();
        await foreach (byte[] bytes in _events.IterateAsync(channelId))
        {
            rows.Add(new TLAdminLogEvent(bytes, 0, bytes.Length));
        }
        return rows;
    }
}
