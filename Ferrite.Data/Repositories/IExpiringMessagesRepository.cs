// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public static class MessageExpiryBox
{
    public const int Common = 0;
    public const int Channel = 1;
}

public interface IExpiringMessagesRepository
{
    bool PutExpiringMessage(TLExpiringMessage message);

    ValueTask<TLExpiringMessage?> GetExpiringMessageAsync(int boxType, long boxId,
        int messageId);

    ValueTask<IReadOnlyCollection<TLExpiringMessage>> GetExpiringMessagesAsync(
        int boxType, long boxId);

    ValueTask<IReadOnlyCollection<TLExpiringMessage>> GetAllExpiringMessagesAsync();

    bool DeleteExpiringMessage(int boxType, long boxId, int messageId);
}
