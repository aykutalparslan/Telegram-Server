// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

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
            // Rolling-upgrade fallback for message rows written before the
            // committed-PTS ledger existed. This scan is only needed until the
            // account's next message writes a ledger row.
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

        // A counter reservation is never client-visible before its durable row.
        // PTS starts at 1, even for a new account with no committed events.
        int visiblePts = Math.Min(reservedPts, Math.Max(1, committedPts));
        // TDLib buffers live PTS updates while its initial getState is pending.
        // If this read overlapped publication, return the cut from query entry;
        // the already-delivered buffered updates then advance it normally.
        return Math.Max(1, visiblePts - publicationsAtEntry);
    }
}
