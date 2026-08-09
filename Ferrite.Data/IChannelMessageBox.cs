// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data;

public interface IChannelMessageBox
{
    /// <summary>
    ///
    /// </summary>
    /// <returns>Current event sequence number for the channel.</returns>
    public ValueTask<int> Pts();
    /// <summary>
    ///  Increments the current event sequence number.
    /// </summary>
    /// <returns>Event sequence number after increment.</returns>
    public ValueTask<int> IncrementPts();
    /// <summary>
    /// Increments the current event sequence number by <paramref name="count"/>.
    /// Used by multi-event updates (e.g. deleting several messages) so the new
    /// pts equals previousPts + pts_count, matching the client gap check
    /// local_pts + pts_count === pts.
    /// </summary>
    /// <param name="count">Number of events generated.</param>
    /// <returns>Event sequence number after the increment.</returns>
    public ValueTask<int> IncrementPts(int count);
    /// <summary>
    /// Increments the channel MessageId counter.
    /// </summary>
    /// <returns>MessageId after the increment.</returns>
    public ValueTask<int> NextMessageId();
    public ValueTask BeginPtsPublication(int ptsCount = 1);
    public ValueTask CompletePtsPublication(int ptsCount = 1);
    public ValueTask<int> PendingPtsPublications();
    public ValueTask<bool> WaitForPtsPublications(
        TimeSpan? timeout = null);
}
