// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services;

/// <summary>
/// The 64-bit list hash a client sends so the server can answer with a
/// `*NotModified` constructor instead of a result it already holds
/// (/api/offsets#hash-generation). The folding order is part of the protocol, so
/// callers must feed the ids in exactly the order the result would carry them.
/// </summary>
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
