// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface INearbyLocationsRepository
{
    bool PutLocation(TLNearbyLocation location);
    ValueTask<TLNearbyLocation?> GetLocationAsync(long userId);
    bool DeleteLocation(long userId);

    /// <summary>
    /// Every published row, including expired ones. Expiry is filtered by the
    /// caller against its own clock so a stale row never needs a sweeper.
    /// </summary>
    IAsyncEnumerable<TLNearbyLocation> IterateLocationsAsync(
        CancellationToken cancellationToken = default);
}
