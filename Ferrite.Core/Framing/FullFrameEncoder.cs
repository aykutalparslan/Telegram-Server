// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using DotNext.Buffers;
using Ferrite.Crypto;

namespace Ferrite.Core.Framing;

public class FullFrameEncoder : IFrameEncoder
{
    private Aes256Ctr? _encryptor;
    private SparseBufferWriter<byte> writer = new SparseBufferWriter<byte>(UnmanagedMemoryPool<byte>.Shared);
    private IncrementalCrc32? _crc32;
    private int _sequence = 0;
    public FullFrameEncoder()
    {
    }
    public FullFrameEncoder(Aes256Ctr encryptor)
    {
        _encryptor = encryptor;
    }

    public ReadOnlySequence<byte> Encode(in ReadOnlySequence<byte> input)
    {
        writer.WriteInt32((int)input.Length, true);
        writer.WriteInt32(_sequence++, true);
        writer.Write(input, false);
        writer.WriteInt32((int)writer.ToReadOnlySequence().GetCrc32(), true);
        var frame = writer.ToReadOnlySequence();
        writer.Clear();
        if (_encryptor != null)
        {
            byte[] frameEncrypted = new byte[frame.Length];
            _encryptor.Transform(frame, frameEncrypted);
            frame = new ReadOnlySequence<byte>(frameEncrypted);
        }
        return frame;
    }

    public ReadOnlySequence<byte> GenerateHead(int length)
    {
        _crc32 = new IncrementalCrc32();
        writer.WriteInt32(length, true);
        writer.WriteInt32(_sequence++, true);
        var frame = writer.ToReadOnlySequence();
        _crc32.AppendData(frame);
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
        writer.WriteInt32((int)_crc32!.Crc32, true);
        var frame = writer.ToReadOnlySequence();
        _crc32 = null;
        writer.Clear();
        if (_encryptor == null) return frame;
        byte[] frameEncrypted = new byte[frame.Length];
        _encryptor.Transform(frame, frameEncrypted);
        frame = new ReadOnlySequence<byte>(frameEncrypted);
        return frame;
    }
}

