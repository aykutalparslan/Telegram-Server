// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data;

public interface IChatIdAllocator
{
    /// <summary>
    /// Allocates the next chat/channel id. Basic groups and channels share
    /// one id namespace, mirroring the user id allocation pattern.
    /// </summary>
    /// <returns>A new nonzero chat id.</returns>
    public ValueTask<long> NextIdAsync();
}
