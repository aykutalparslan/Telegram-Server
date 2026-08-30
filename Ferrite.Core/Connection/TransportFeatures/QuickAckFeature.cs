// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using System.Buffers.Binary;
using DotNext.Buffers;

namespace Ferrite.Core.Connection.TransportFeatures;

public class QuickAckFeature : IQuickAckFeature
{
    public ReadOnlySequence<byte> GenerateQuickAck(int ack, MTProtoTransport transport)
    {
        BufferWriterSlim<byte> writer = new(stackalloc byte[4]);
        writer.Clear();
        ack |= 1 << 31;
        if (transport == MTProtoTransport.Abridged)
        {
            ack = BinaryPrimitives.ReverseEndianness(ack);
        }
        writer.WriteInt32(ack, true);
        var msg = writer.WrittenSpan;
        return new ReadOnlySequence<byte>(writer.WrittenSpan.ToArray());
    }
}