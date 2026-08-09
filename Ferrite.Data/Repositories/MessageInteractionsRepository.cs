// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class MessageInteractionsRepository : IMessageInteractionsRepository
{
    private readonly IKVStore _receipts;
    private readonly IKVStore _counters;

    public MessageInteractionsRepository(IKVStore receipts, IKVStore counters)
    {
        _receipts = receipts;
        _counters = counters;
        receipts.SetSchema(new TableDefinition("ferrite", "message_view_receipts",
            new KeyDefinition("pk",
                new DataColumn { Name = "box_type", Type = DataType.Int },
                new DataColumn { Name = "box_id", Type = DataType.Long },
                new DataColumn { Name = "message_id", Type = DataType.Int },
                new DataColumn { Name = "user_id", Type = DataType.Long })));
        counters.SetSchema(new TableDefinition("ferrite", "message_interactions",
            new KeyDefinition("pk",
                new DataColumn { Name = "box_type", Type = DataType.Int },
                new DataColumn { Name = "box_id", Type = DataType.Long },
                new DataColumn { Name = "message_id", Type = DataType.Int })));
    }

    public bool PutViewReceipt(TLMessageViewReceipt receipt)
    {
        var info = receipt.AsMessageViewReceipt();
        return _receipts.Put(receipt.AsSpan().ToArray(), info.BoxType, info.BoxId,
            info.MessageId, info.UserId);
    }

    public async ValueTask<TLMessageViewReceipt?> GetViewReceiptAsync(
        MessageIdentity identity, long userId)
    {
        byte[]? bytes = await _receipts.GetAsync(identity.BoxType, identity.BoxId,
            identity.MessageId, userId);
        return bytes is { Length: > 0 }
            ? new TLMessageViewReceipt(bytes, 0, bytes.Length)
            : null;
    }

    public async ValueTask<IReadOnlyCollection<TLMessageViewReceipt>> GetViewReceiptsAsync(
        MessageIdentity identity)
    {
        List<TLMessageViewReceipt> receipts = new();
        await foreach (byte[] bytes in _receipts.IterateAsync(identity.BoxType,
                           identity.BoxId, identity.MessageId))
        {
            receipts.Add(new TLMessageViewReceipt(bytes, 0, bytes.Length));
        }
        return receipts;
    }

    public bool PutInteractionInfo(TLMessageInteractionInfo info)
    {
        var view = info.AsMessageInteractionInfo();
        return _counters.Put(info.AsSpan().ToArray(), view.BoxType, view.BoxId,
            view.MessageId);
    }

    public async ValueTask<TLMessageInteractionInfo?> GetInteractionInfoAsync(
        MessageIdentity identity)
    {
        byte[]? bytes = await _counters.GetAsync(identity.BoxType, identity.BoxId,
            identity.MessageId);
        return bytes is { Length: > 0 }
            ? new TLMessageInteractionInfo(bytes, 0, bytes.Length)
            : null;
    }

    public bool DeleteInteractions(MessageIdentity identity)
    {
        bool receipts = _receipts.Delete(identity.BoxType, identity.BoxId,
            identity.MessageId);
        bool counters = _counters.Delete(identity.BoxType, identity.BoxId,
            identity.MessageId);
        return receipts || counters;
    }
}
