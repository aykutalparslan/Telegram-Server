// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using Ferrite.Core.Connection;
using Ferrite.Core.Execution;

namespace Ferrite.Core;

public interface IProtoHandler
{
    public IMTProtoSession? Session { get; set; }
    public ProtoMessage DecryptMessage(in ReadOnlySequence<byte> bytes);
    public ProtoMessage ReadPlaintextMessage(in ReadOnlySequence<byte> bytes);
    public ReadOnlySequence<byte> EncryptMessage(MTProtoMessage message);
    public ReadOnlySequence<byte> PreparePlaintextMessage(MTProtoMessage message);
    public ValueTask<StreamingProtoMessage> ProcessIncomingStreamAsync(ReadOnlySequence<byte> bytes, bool hasMore);
    public ValueTask<ValueTuple<int, ReadOnlySequence<byte>, MTProtoPipe>> GenerateOutgoingStream(IFileOwner? message);
}