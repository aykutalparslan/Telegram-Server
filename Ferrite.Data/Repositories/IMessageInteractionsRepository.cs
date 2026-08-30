// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public static class MessageIdentityBox
{
    public const int Logical = 0;
    public const int Channel = 1;
}

public readonly record struct MessageIdentity(int BoxType, long BoxId, int MessageId)
{
    public static MessageIdentity ForLogical(long logicalId) =>
        new(MessageIdentityBox.Logical, logicalId, 0);

    public static MessageIdentity ForChannel(long channelId, int messageId) =>
        new(MessageIdentityBox.Channel, channelId, messageId);
}

public interface IMessageInteractionsRepository
{
    bool PutViewReceipt(TLMessageViewReceipt receipt);
    ValueTask<TLMessageViewReceipt?> GetViewReceiptAsync(MessageIdentity identity,
        long userId);
    ValueTask<IReadOnlyCollection<TLMessageViewReceipt>> GetViewReceiptsAsync(
        MessageIdentity identity);

    bool PutInteractionInfo(TLMessageInteractionInfo info);
    ValueTask<TLMessageInteractionInfo?> GetInteractionInfoAsync(
        MessageIdentity identity);

    bool DeleteInteractions(MessageIdentity identity);
}
