// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

using xxHash;

public class ArrayEqualityComparer : IEqualityComparer<byte[]>
{
    public bool Equals(byte[]? x, byte[]? y)
    {
        if (x == null && y == null)
        {
            return true;
        }
        else if(x == null || y == null)
        {
            return false;
        }
        return x.GetXxHash64() == y.GetXxHash64();
    }

    public int GetHashCode(byte[] obj)
    {
        return (int)obj.GetXxHash64();
    }
}