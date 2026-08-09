// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;

namespace Ferrite.Core.Framing;

public interface IFrameDecoder
{
    /// <summary>
    /// Decodes a full or partial MTProto frame
    /// </summary>
    /// <param name="bytes">Sequence of bytes to read from.</param>
    /// <param name="frame">Full or partial frame data.</param>
    /// <param name="isStream">If the frame belongs to an API method
    /// whose body is streamed from the pipe instead of buffered.</param>
    /// <returns>True if there's more data to process.</returns>
    bool Decode(ReadOnlySequence<byte> bytes, out ReadOnlySequence<byte> frame, 
        out bool isStream, out bool requiresQuickAck, out SequencePosition position);
}

