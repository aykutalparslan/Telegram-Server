// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using Ferrite.Services;

namespace Ferrite.Core.Framing
{
    public interface ITransportDetector
    {
        MTProtoTransport DetectTransport(ReadOnlySequence<byte> bytes,
            out IFrameDecoder? decoder, out IFrameEncoder? encoder, 
            out SequencePosition sequencePosition);
    }
}

