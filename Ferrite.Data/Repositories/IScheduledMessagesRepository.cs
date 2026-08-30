// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public static class ScheduledMessageState
{
    public const int Queued = 0;

    public const int Claimed = 1;
}

public interface IScheduledMessagesRepository
{
    bool PutScheduledMessage(TLScheduledMessage message);

    ValueTask<TLScheduledMessage?> GetScheduledMessageAsync(long ownerUserId,
        int peerType, long peerId, int scheduledId);

    ValueTask<IReadOnlyCollection<TLScheduledMessage>> GetScheduledMessagesAsync(
        long ownerUserId, int peerType, long peerId);

    ValueTask<IReadOnlyCollection<TLScheduledMessage>> GetAllScheduledMessagesAsync();

    bool DeleteScheduledMessage(long ownerUserId, int peerType, long peerId,
        int scheduledId);
}
