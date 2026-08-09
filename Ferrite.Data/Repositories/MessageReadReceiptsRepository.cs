// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class MessageReadReceiptsRepository : IMessageReadReceiptsRepository
{
    private readonly IKVStore _receipts;

    public MessageReadReceiptsRepository(IKVStore receipts)
    {
        _receipts = receipts;
        receipts.SetSchema(new TableDefinition("ferrite", "message_read_receipts",
            new KeyDefinition("pk",
                new DataColumn { Name = "box_type", Type = DataType.Int },
                new DataColumn { Name = "box_id", Type = DataType.Long },
                new DataColumn { Name = "message_id", Type = DataType.Int },
                new DataColumn { Name = "user_id", Type = DataType.Long })));
    }

    public bool PutReadReceipt(TLMessageReadReceipt receipt)
    {
        var info = receipt.AsMessageReadReceipt();
        return _receipts.Put(receipt.AsSpan().ToArray(), info.BoxType, info.BoxId,
            info.MessageId, info.UserId);
    }

    public async ValueTask<TLMessageReadReceipt?> GetReadReceiptAsync(
        MessageIdentity identity, long userId)
    {
        byte[]? bytes = await _receipts.GetAsync(identity.BoxType, identity.BoxId,
            identity.MessageId, userId);
        return bytes is { Length: > 0 }
            ? new TLMessageReadReceipt(bytes, 0, bytes.Length)
            : null;
    }

    public async ValueTask<IReadOnlyCollection<TLMessageReadReceipt>> GetReadReceiptsAsync(
        MessageIdentity identity)
    {
        List<TLMessageReadReceipt> receipts = new();
        await foreach (byte[] bytes in _receipts.IterateAsync(identity.BoxType,
                           identity.BoxId, identity.MessageId))
        {
            receipts.Add(new TLMessageReadReceipt(bytes, 0, bytes.Length));
        }
        return receipts;
    }

    public bool DeleteReadReceipts(MessageIdentity identity) =>
        _receipts.Delete(identity.BoxType, identity.BoxId, identity.MessageId);
}
