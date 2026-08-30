// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Updates;

internal static class CommonUpdatesState
{
    public static async ValueTask<int> GetCommittedPts(
        IUpdatesStateRepository updatesStateRepository,
        IMessageRepository messageRepository,
        IUpdatesContext context, long userId)
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

        int visiblePts = Math.Min(reservedPts, Math.Max(1, committedPts));
        int deliveredPts = await context.DeliveredPts();
        if (deliveredPts > 0)
        {
            visiblePts = Math.Min(visiblePts, deliveredPts);
        }
        return Math.Max(1, visiblePts - publicationsAtEntry);
    }
}
