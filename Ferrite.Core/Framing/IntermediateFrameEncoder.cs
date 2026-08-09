// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using DotNext.Buffers;
using Ferrite.Crypto;

namespace Ferrite.Core.Framing;

public class IntermediateFrameEncoder : IFrameEncoder
{
    private Aes256Ctr? _encryptor;
    private SparseBufferWriter<byte> writer = new SparseBufferWriter<byte>(UnmanagedMemoryPool<byte>.Shared);
    public IntermediateFrameEncoder()
    {
    }
    public IntermediateFrameEncoder(Aes256Ctr encryptor)
    {
        _encryptor = encryptor;
    }

    public ReadOnlySequence<byte> Encode(in ReadOnlySequence<byte> input)
    {
        writer.WriteInt32((int)input.Length, true);
        writer.Write(input, false);
        var frame = writer.ToReadOnlySequence();
        writer.Clear();
        if (_encryptor == null) return frame;
        byte[] frameEncrypted = new byte[frame.Length];
        _encryptor.Transform(frame, frameEncrypted);
        frame = new ReadOnlySequence<byte>(frameEncrypted);
        return frame;
    }

    public ReadOnlySequence<byte> GenerateHead(int length)
    {
        writer.WriteInt32(length, true);
        var frame = writer.ToReadOnlySequence();
        writer.Clear();
        return frame;
    }

    public ReadOnlySequence<byte> EncodeBlock(in ReadOnlySequence<byte> input)
    {
        writer.Write(input, false);
        var frame = writer.ToReadOnlySequence();
        writer.Clear();
        if (_encryptor == null) return frame;
        byte[] frameEncrypted = new byte[frame.Length];
        _encryptor.Transform(frame, frameEncrypted);
        frame = new ReadOnlySequence<byte>(frameEncrypted);
        return frame;
    }

    public ReadOnlySequence<byte> EncodeTail()
    {
        return new ReadOnlySequence<byte>();
    }
}
