// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using System.IO.Pipelines;

namespace Ferrite.Core.Connection.TransportFeatures;

public interface IWebSocketFeature
{
    public bool WebSocketHandshakeCompleted { get; }
    public PipeReader WebSocketReader { get; }
    public HandshakeResponse ProcessWebSocketHandshake(ReadOnlySequence<byte> data);
    public ValueTask<SequencePosition> DecodeWebSocketData(ReadOnlySequence<byte> buffer);
    public ReadOnlySequence<byte> GenerateWebSocketHeader(int length);
}