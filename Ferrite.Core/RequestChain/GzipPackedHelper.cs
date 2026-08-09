// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.IO.Compression;
using DotNext.Buffers;
using Ferrite.TL;

namespace Ferrite.Core.RequestChain;

internal static class GzipPackedHelper
{
    public static TLBytes Unpack(TLBytes input)
    {
        var gzipPacked = new TL.mtproto.GzipPacked(input.AsSpan());
        ReadOnlySpan<byte> packedData = gzipPacked.PackedData;
        bool isGzip = packedData.Length >= 2 && packedData[0] == 0x1f && packedData[1] == 0x8b;

        using var compressed = new MemoryStream(packedData.ToArray());
        using Stream stream = isGzip
            ? new GZipStream(compressed, CompressionMode.Decompress)
            : new ZLibStream(compressed, CompressionMode.Decompress);
        using var decompressed = new MemoryStream();
        stream.CopyTo(decompressed);

        byte[] bytes = decompressed.ToArray();
        var memoryOwner = UnmanagedMemoryPool<byte>.Shared.Rent(bytes.Length);
        bytes.AsSpan().CopyTo(memoryOwner.Memory.Span);
        return new TLBytes(memoryOwner, 0, bytes.Length);
    }
}
