// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;

namespace Ferrite.Core.Framing;

public interface IFrameDecoder
{
    bool Decode(ReadOnlySequence<byte> bytes, out ReadOnlySequence<byte> frame, 
        out bool isStream, out bool requiresQuickAck, out SequencePosition position);
}

