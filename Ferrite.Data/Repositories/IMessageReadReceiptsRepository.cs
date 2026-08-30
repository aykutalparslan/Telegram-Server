// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IMessageReadReceiptsRepository
{
    bool PutReadReceipt(TLMessageReadReceipt receipt);
    ValueTask<TLMessageReadReceipt?> GetReadReceiptAsync(MessageIdentity identity,
        long userId);
    ValueTask<IReadOnlyCollection<TLMessageReadReceipt>> GetReadReceiptsAsync(
        MessageIdentity identity);
    bool DeleteReadReceipts(MessageIdentity identity);
}
