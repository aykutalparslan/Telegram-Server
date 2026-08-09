// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

/// <summary>
/// Stores per-message reactions, the cross-copy message id mapping, and per-user
/// reaction settings. Reaction rows are keyed by the message box that stores the
/// reacted-to message: box type <see cref="MessageReactionBox.Common"/> uses the
/// copy owner's user id as the box id (one row per common-box copy), and
/// <see cref="MessageReactionBox.Channel"/> uses the channel id (single shared row).
/// </summary>
public static class MessageReactionBox
{
    public const int Common = 0;
    public const int Channel = 1;
}

public interface IMessageReactionsRepository
{
    bool PutReaction(TLMessageReactionInfo reaction);
    ValueTask<TLMessageReactionInfo?> GetReactionAsync(int boxType, long boxId,
        int messageId, long userId);
    ValueTask<IReadOnlyCollection<TLMessageReactionInfo>> GetReactionsAsync(int boxType,
        long boxId, int messageId);
    ValueTask<IReadOnlyCollection<TLMessageReactionInfo>> GetBoxReactionsAsync(int boxType,
        long boxId);
    ValueTask<IReadOnlyCollection<TLMessageReactionInfo>> GetUserReactionsAsync(int boxType,
        long boxId, long userId);
    bool DeleteReaction(int boxType, long boxId, int messageId, long userId);
    bool DeleteReactions(int boxType, long boxId, int messageId);
    bool DeleteBoxReactions(int boxType, long boxId);

    /// <summary>
    /// Records that the message copy stored under <paramref name="copy"/>'s user id
    /// with its per-user message id belongs to the shared logical message.
    /// </summary>
    bool PutMessageCopy(TLMessageCopyInfo copy);
    ValueTask<IReadOnlyCollection<TLMessageCopyInfo>> GetMessageCopiesAsync(long logicalId);
    ValueTask<TLMessageCopyInfo?> GetCopyByOwnerMessageAsync(long userId, int messageId);
    bool DeleteMessageCopies(long logicalId);

    bool PutReactionSettings(TLReactionSettingsInfo settings);
    ValueTask<TLReactionSettingsInfo?> GetReactionSettingsAsync(long userId);
}
