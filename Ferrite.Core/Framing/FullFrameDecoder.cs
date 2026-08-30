// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using Ferrite.Crypto;

namespace Ferrite.Core.Framing;

public class FullFrameDecoder : FrameDecoderBase
{
    public FullFrameDecoder(IMTProtoService mtproto) : base(mtproto)
    {
        LengthBytesToSkip = 4;
        Header = 4;
        Tail = 4;
    }

    public FullFrameDecoder(Aes256Ctr decryptor, IMTProtoService mtproto) : base(decryptor, mtproto)
    {
        LengthBytesToSkip = 4;
        Header = 4;
        Tail = 4;
    }

    protected override bool DecodeLength(ref SequenceReader<byte> reader, out bool emptyFrame)
    {
        if (reader.Remaining < 4)
        {
            emptyFrame = true;
            return false;
        }

        reader.TryCopyTo(LengthBytes);
        Decryptor?.Transform(LengthBytes);
        bool requiresQuickAck = CheckRequiresQuickAck(LengthBytes, 3);

        Length = (LengthBytes[0]) |
                 (LengthBytes[1] << 8) |
                 (LengthBytes[2] << 16) |
                 (LengthBytes[3] << 24);
        reader.Advance(4);
        emptyFrame = false;
        return requiresQuickAck;
    }
}

