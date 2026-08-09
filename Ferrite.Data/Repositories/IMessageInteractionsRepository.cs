// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

/// <summary>
/// Box discriminator for message-identity keyed interaction and receipt rows.
/// Unlike <see cref="MessageReactionBox"/>, which keys one row per common-box
/// copy, these rows are keyed by the identity a viewer count or a read receipt
/// must be shared across: the logical message behind every common-box copy, or
/// the single shared channel post.
/// </summary>
public static class MessageIdentityBox
{
    public const int Logical = 0;
    public const int Channel = 1;
}

/// <summary>
/// Identity of one logical message. Private and basic-group messages exist as
/// per-owner copies with different local ids, so their identity is the logical id
/// recorded by <see cref="IMessageReactionsRepository.PutMessageCopy"/> and the
/// message-id component is unused. A channel post exists once, so its identity is
/// the channel id plus the channel message id.
/// </summary>
public readonly record struct MessageIdentity(int BoxType, long BoxId, int MessageId)
{
    public static MessageIdentity ForLogical(long logicalId) =>
        new(MessageIdentityBox.Logical, logicalId, 0);

    public static MessageIdentity ForChannel(long channelId, int messageId) =>
        new(MessageIdentityBox.Channel, channelId, messageId);
}

/// <summary>
/// Durable view/forward counters plus the per-viewer receipt rows that make
/// incrementing a view count idempotent for a given viewer. The counter row is
/// what clients are served; the receipt rows are the idempotence index and only
/// a first receipt for a viewer advances the counter.
/// </summary>
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
