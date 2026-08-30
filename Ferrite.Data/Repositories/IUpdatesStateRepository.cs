// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

public interface IUpdatesStateRepository
{
    bool PutPts(long userId, int pts);
    ValueTask<int> GetPtsAsync(long userId);
}
