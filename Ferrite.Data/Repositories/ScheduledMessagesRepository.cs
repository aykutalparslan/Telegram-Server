// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class ScheduledMessagesRepository : IScheduledMessagesRepository
{
    private readonly IKVStore _scheduled;

    public ScheduledMessagesRepository(IKVStore scheduled)
    {
        _scheduled = scheduled;
        scheduled.SetSchema(new TableDefinition("ferrite", "scheduled_messages",
            new KeyDefinition("pk",
                new DataColumn { Name = "owner_user_id", Type = DataType.Long },
                new DataColumn { Name = "peer_type", Type = DataType.Int },
                new DataColumn { Name = "peer_id", Type = DataType.Long },
                new DataColumn { Name = "scheduled_id", Type = DataType.Int })));
    }

    public bool PutScheduledMessage(TLScheduledMessage message)
    {
        var row = message.AsScheduledMessage();
        return _scheduled.Put(message.AsSpan().ToArray(), row.OwnerUserId,
            row.PeerType, row.PeerId, row.ScheduledId);
    }

    public async ValueTask<TLScheduledMessage?> GetScheduledMessageAsync(
        long ownerUserId, int peerType, long peerId, int scheduledId)
    {
        byte[]? bytes = await _scheduled.GetAsync(ownerUserId, peerType, peerId,
            scheduledId);
        return bytes is { Length: > 0 }
            ? new TLScheduledMessage(bytes, 0, bytes.Length)
            : null;
    }

    public async ValueTask<IReadOnlyCollection<TLScheduledMessage>>
        GetScheduledMessagesAsync(long ownerUserId, int peerType, long peerId)
    {
        List<TLScheduledMessage> messages = new();
        await foreach (byte[] bytes in _scheduled.IterateAsync(ownerUserId,
                           peerType, peerId))
        {
            messages.Add(new TLScheduledMessage(bytes, 0, bytes.Length));
        }
        return messages;
    }

    public async ValueTask<IReadOnlyCollection<TLScheduledMessage>>
        GetAllScheduledMessagesAsync()
    {
        List<TLScheduledMessage> messages = new();
        await foreach (byte[] bytes in _scheduled.IterateAsync())
        {
            messages.Add(new TLScheduledMessage(bytes, 0, bytes.Length));
        }
        return messages;
    }

    public bool DeleteScheduledMessage(long ownerUserId, int peerType, long peerId,
        int scheduledId) =>
        _scheduled.Delete(ownerUserId, peerType, peerId, scheduledId);
}
