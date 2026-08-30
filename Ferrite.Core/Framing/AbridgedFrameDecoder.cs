// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using Ferrite.Crypto;

namespace Ferrite.Core.Framing;

public class AbridgedFrameDecoder : FrameDecoderBase
{
    public AbridgedFrameDecoder(IMTProtoService mtproto) : base(mtproto)
    {
    }

    public AbridgedFrameDecoder(Aes256Ctr decryptor, IMTProtoService mtproto) : base(decryptor, mtproto)
    {
    }
    
    protected override bool DecodeLength(ref SequenceReader<byte> reader, out bool emptyFrame)
    {
        if (reader.Remaining == 0)
        {
            emptyFrame = true;
            return false;
        }
        GetFirstLengthByte(ref reader);
        if (LengthBytes[0] == 127 && reader.Remaining < 3)
        {
            emptyFrame = true;
            return false;
        }
        bool requiresQuickAck = CheckRequiresQuickAck(LengthBytes, 0);
        if (LengthBytes[0] < 127)
        {
            Length = LengthBytes[0] * 4;
        }
        else if (LengthBytes[0] == 127)
        {
            reader.TryCopyTo(LengthBytes.AsSpan().Slice(1, 3));
            reader.Advance(3);
            Decryptor?.Transform(LengthBytes.AsSpan().Slice(1, 3));
            requiresQuickAck = CheckRequiresQuickAck(LengthBytes, 3);
            Length = (LengthBytes[1]) |
                      (LengthBytes[2] << 8) |
                      (LengthBytes[3] << 16);
            Length *= 4;
        }

        emptyFrame = false;
        return requiresQuickAck;
    }

    private void GetFirstLengthByte(ref SequenceReader<byte> reader)
    {
        if (LengthBytes[0] == 0)
        {
            reader.TryCopyTo(LengthBytes.AsSpan()[..1]);
            reader.Advance(1);
            Decryptor?.Transform(LengthBytes.AsSpan()[..1]);
        }
    }
}

