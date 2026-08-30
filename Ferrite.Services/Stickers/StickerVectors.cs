// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;

namespace Ferrite.Services.Stickers;

internal static class StickerVectors
{
    public static VectorOfLong ToLongVector(IEnumerable<long> values)
    {
        var vector = new VectorOfLong();
        foreach (long value in values) vector.Append(value);
        return vector;
    }

    public static VectorOfInt ToIntVector(IEnumerable<int> values)
    {
        var vector = new VectorOfInt();
        foreach (int value in values) vector.Append(value);
        return vector;
    }

    public static Vector CopyObjectVector(Vector source)
    {
        var result = new Vector();
        int count = source.Count;
        for (int i = 0; i < count; i++)
        {
            result.AppendTLObject(source.ReadTLObject());
        }
        return result;
    }

    public static TLBytes CopyVector(Vector vector)
    {
        byte[] bytes = vector.ToReadOnlySpan().ToArray();
        return new TLBytes(bytes, 0, bytes.Length);
    }
}
