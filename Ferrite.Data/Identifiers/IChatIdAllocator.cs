// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Identifiers;

public interface IChatIdAllocator
{
    public ValueTask<long> NextIdAsync();
}
