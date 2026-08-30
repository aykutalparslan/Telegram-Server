// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

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

    bool PutMessageCopy(TLMessageCopyInfo copy);
    ValueTask<IReadOnlyCollection<TLMessageCopyInfo>> GetMessageCopiesAsync(long logicalId);
    ValueTask<TLMessageCopyInfo?> GetCopyByOwnerMessageAsync(long userId, int messageId);
    bool DeleteMessageCopies(long logicalId);

    bool PutReactionSettings(TLReactionSettingsInfo settings);
    ValueTask<TLReactionSettingsInfo?> GetReactionSettingsAsync(long userId);
}
