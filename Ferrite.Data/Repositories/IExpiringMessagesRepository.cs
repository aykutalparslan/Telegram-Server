// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public static class MessageExpiryBox
{
    public const int Common = 0;
    public const int Channel = 1;
}

/// <summary>
/// The durable auto-delete index, keyed per stored COPY: a common-box message
/// exists once in each participant's own box with that box's own local id and pts,
/// and a channel post exists once for the whole channel. Only rows written here are
/// ever considered due, so a message that predates a TTL setting is never touched.
/// </summary>
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
