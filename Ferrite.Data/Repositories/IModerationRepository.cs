// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IModerationRepository
{
    bool PutActionBarState(TLPeerActionBarState state);
    ValueTask<TLPeerActionBarState?> GetActionBarStateAsync(long userId,
        int peerType, long peerId);
    bool DeleteActionBarState(long userId, int peerType, long peerId);

    /// <summary>
    /// Appends an accepted report. Reports are immutable, so a stored row is
    /// never rewritten and the report id makes a repeat submission its own row.
    /// </summary>
    bool PutReport(TLModerationReport report);
    ValueTask<TLModerationReport?> GetReportAsync(long reporterUserId, long reportId);
    IAsyncEnumerable<TLModerationReport> IterateReportsAsync(long reporterUserId,
        CancellationToken cancellationToken = default);
}
