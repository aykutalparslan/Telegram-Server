// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class ChannelContentReadsRepository : IChannelContentReadsRepository
{
    private readonly IKVStore _reads;

    public ChannelContentReadsRepository(IKVStore reads)
    {
        _reads = reads;
        reads.SetSchema(new TableDefinition("ferrite", "channel_content_reads",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "channel_id", Type = DataType.Long },
                new DataColumn { Name = "message_id", Type = DataType.Int })));
    }

    public bool PutContentRead(TLChannelContentRead read)
    {
        var info = read.AsChannelContentRead();
        return _reads.Put(read.AsSpan().ToArray(), info.UserId, info.ChannelId,
            info.MessageId);
    }

    public async ValueTask<TLChannelContentRead?> GetContentReadAsync(long userId,
        long channelId, int messageId)
    {
        byte[]? bytes = await _reads.GetAsync(userId, channelId, messageId);
        return bytes is { Length: > 0 }
            ? new TLChannelContentRead(bytes, 0, bytes.Length)
            : null;
    }
}
