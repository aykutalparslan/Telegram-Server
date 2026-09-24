// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Updates;

internal static class CommonUpdatesState
{
    public static ValueTask<int> GetCommittedPts(
        IUpdatesStateRepository updatesStateRepository,
        IMessageRepository messageRepository,
        IUpdatesContext context, long userId) =>
        ReadPts(updatesStateRepository, messageRepository, context, userId,
            capAtDelivered: false);

    public static ValueTask<int> GetDeliveredPts(
        IUpdatesStateRepository updatesStateRepository,
        IMessageRepository messageRepository,
        IUpdatesContext context, long userId) =>
        ReadPts(updatesStateRepository, messageRepository, context, userId,
            capAtDelivered: true);

    private static async ValueTask<int> ReadPts(
        IUpdatesStateRepository updatesStateRepository,
        IMessageRepository messageRepository,
        IUpdatesContext context, long userId, bool capAtDelivered)
    {
        int publicationsAtEntry = await context.PendingPtsPublications();
        await context.WaitForPtsPublications();
        int reservedPts = await context.Pts();
        if (reservedPts <= 1)
        {
            return reservedPts;
        }

        int committedPts = await updatesStateRepository
            .GetPtsAsync(userId);
        if (committedPts == 0)
        {
            IReadOnlyCollection<TLSavedMessage> rows =
                await messageRepository.GetMessagesAsync(userId);
            foreach (TLSavedMessage row in rows)
            {
                using (row)
                {
                    committedPts = Math.Max(committedPts,
                        row.AsSavedMessage().Pts);
                }
            }
        }

        committedPts = Math.Max(1, committedPts);
        committedPts = Math.Max(committedPts, await context.ExtendCommittedPts(committedPts));
        int visiblePts = Math.Min(reservedPts, committedPts);
        int deliveredPts = capAtDelivered ? await context.DeliveredPts() : 0;
        if (deliveredPts > 0)
        {
            visiblePts = Math.Min(visiblePts, deliveredPts);
        }
        return Math.Max(1, visiblePts - publicationsAtEntry);
    }
}
