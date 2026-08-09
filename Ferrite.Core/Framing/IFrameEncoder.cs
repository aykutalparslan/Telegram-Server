// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;

namespace Ferrite.Core.Framing;

public interface IFrameEncoder
{
    ReadOnlySequence<byte> Encode(in ReadOnlySequence<byte> input);
    ReadOnlySequence<byte> GenerateHead(int length);
    ReadOnlySequence<byte> EncodeBlock(in ReadOnlySequence<byte> input);
    ReadOnlySequence<byte> EncodeTail();
}


