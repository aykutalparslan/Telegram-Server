// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

/// <summary>
/// Dated per-reader receipts written when a message is first marked read. The
/// first receipt for a reader wins, so an outbox read date is the moment the
/// recipient actually read the message rather than the query time. Expiry is a
/// service-level rule against the stored date (`chat_read_mark_expire_period`);
/// the repository keeps the rows until they are deleted with their message.
/// </summary>
public interface IMessageReadReceiptsRepository
{
    bool PutReadReceipt(TLMessageReadReceipt receipt);
    ValueTask<TLMessageReadReceipt?> GetReadReceiptAsync(MessageIdentity identity,
        long userId);
    ValueTask<IReadOnlyCollection<TLMessageReadReceipt>> GetReadReceiptsAsync(
        MessageIdentity identity);
    bool DeleteReadReceipts(MessageIdentity identity);
}
