// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using DotNext.Buffers;

namespace Ferrite.Core.Connection.TransportFeatures;

public class TransportErrorFeature : ITransportErrorFeature
{
    public ReadOnlySequence<byte> GenerateTransportError(int errorCode)
    {
        BufferWriterSlim<byte> writer = new(stackalloc byte[4]);
        writer.WriteInt32(-1 * errorCode, true);
        var msg = writer.WrittenSpan;
        return new ReadOnlySequence<byte>(writer.WrittenSpan.ToArray());
    }
}