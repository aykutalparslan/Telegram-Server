// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Common;

public static class TelegramListHash
{
    public static long Compute(IEnumerable<long> ids)
    {
        ulong hash = 0;
        foreach (long id in ids)
        {
            hash ^= hash >> 21;
            hash ^= hash << 35;
            hash ^= hash >> 4;
            hash += unchecked((ulong)id);
        }
        return unchecked((long)hash);
    }
}
