// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL;

public static class TLExtensions
{
    public static Vector ToVector(this ICollection<TLBytes> rules)
    {
        var vec = new Vector();
        foreach (var r in rules)
        {
            vec.AppendTLObject(r.AsSpan());
        }

        return vec;
    }
}