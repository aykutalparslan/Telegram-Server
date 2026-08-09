// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class ExpiringMessagesRepository : IExpiringMessagesRepository
{
    private readonly IKVStore _expiring;

    public ExpiringMessagesRepository(IKVStore expiring)
    {
        _expiring = expiring;
        expiring.SetSchema(new TableDefinition("ferrite", "expiring_messages",
            new KeyDefinition("pk",
                new DataColumn { Name = "box_type", Type = DataType.Int },
                new DataColumn { Name = "box_id", Type = DataType.Long },
                new DataColumn { Name = "message_id", Type = DataType.Int })));
    }

    public bool PutExpiringMessage(TLExpiringMessage message)
    {
        var row = message.AsExpiringMessage();
        return _expiring.Put(message.AsSpan().ToArray(), row.BoxType, row.BoxId,
            row.MessageId);
    }

    public async ValueTask<TLExpiringMessage?> GetExpiringMessageAsync(int boxType,
        long boxId, int messageId)
    {
        byte[]? bytes = await _expiring.GetAsync(boxType, boxId, messageId);
        return bytes is { Length: > 0 }
            ? new TLExpiringMessage(bytes, 0, bytes.Length)
            : null;
    }

    public async ValueTask<IReadOnlyCollection<TLExpiringMessage>>
        GetExpiringMessagesAsync(int boxType, long boxId)
    {
        List<TLExpiringMessage> messages = new();
        await foreach (byte[] bytes in _expiring.IterateAsync(boxType, boxId))
        {
            messages.Add(new TLExpiringMessage(bytes, 0, bytes.Length));
        }
        return messages;
    }

    public async ValueTask<IReadOnlyCollection<TLExpiringMessage>>
        GetAllExpiringMessagesAsync()
    {
        List<TLExpiringMessage> messages = new();
        await foreach (byte[] bytes in _expiring.IterateAsync())
        {
            messages.Add(new TLExpiringMessage(bytes, 0, bytes.Length));
        }
        return messages;
    }

    public bool DeleteExpiringMessage(int boxType, long boxId, int messageId) =>
        _expiring.Delete(boxType, boxId, messageId);
}
