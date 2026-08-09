// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

/// <summary>
/// Durable committed high-water marks for the common updates PTS sequence.
/// A reservation in an external counter is not visible client state until the
/// corresponding unit-of-work batch containing this row has committed.
/// </summary>
public interface IUpdatesStateRepository
{
    bool PutPts(long userId, int pts);
    ValueTask<int> GetPtsAsync(long userId);
}
