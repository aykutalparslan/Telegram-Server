// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using Ferrite.Core.Connection.TransportFeatures;
using Ferrite.Core.Framing;
using Ferrite.Services;

namespace Ferrite.Core.Connection;

public class ProtoTransport : IQuickAckFeature,
    ITransportErrorFeature, IFrameEncoder, IFrameDecoder
{
    public MTProtoTransport TransportType { get; private set; }
    private readonly ITransportDetector _transportDetector;
    private IFrameDecoder? _decoder;
    private IFrameEncoder? _encoder;
    private readonly IQuickAckFeature _quickAck;
    private readonly ITransportErrorFeature _transportError;
    private readonly IWebSocketFeature _webSocket;

    public ProtoTransport(ITransportDetector detector,
        IQuickAckFeature quickAck,
        ITransportErrorFeature transportError,
        IWebSocketFeature webSocket)
    {
        TransportType = MTProtoTransport.Unknown;
        _transportDetector = detector;
        _quickAck = quickAck;
        _transportError = transportError;
        _webSocket = webSocket;
    }

    public bool WebSocketHandshakeCompleted => _webSocket.WebSocketHandshakeCompleted;
    public async ValueTask<ReadOnlySequence<byte>> ReadFromWebSocketAsync()
    {
        var wsResult = await _webSocket.WebSocketReader.ReadAsync();
        return wsResult.Buffer;
    }
    public void AdvanceWebSocketTo(SequencePosition position)
    {
        _webSocket.WebSocketReader.AdvanceTo(position);
    }
    public HandshakeResponse ProcessWebSocketHandshake(ReadOnlySequence<byte> data)
    {
        return _webSocket.ProcessWebSocketHandshake(data);
    }

    public async ValueTask<SequencePosition> DecodeWebSocketData(ReadOnlySequence<byte> buffer)
    {
        return await _webSocket.DecodeWebSocketData(buffer);
    }

    public ReadOnlySequence<byte> GenerateWebSocketHeader(int length)
    {
        return _webSocket.GenerateWebSocketHeader(length);
    }

    public ReadOnlySequence<byte> GenerateQuickAck(int ack, MTProtoTransport transport)
    {
        return _quickAck.GenerateQuickAck(ack, transport);
    }

    public ReadOnlySequence<byte> GenerateTransportError(int errorCode)
    {
        return _transportError.GenerateTransportError(errorCode);
    }

    public void DetectTransport(ReadOnlySequence<byte> bytes, out SequencePosition sequencePosition)
    {
        TransportType = _transportDetector.DetectTransport(bytes, out _decoder, out _encoder, out sequencePosition);
    }

    public ReadOnlySequence<byte> Encode(in ReadOnlySequence<byte> input)
    {
        return _encoder?.Encode(input) ?? input;
    }

    public ReadOnlySequence<byte> GenerateHead(int length)
    {
        return _encoder?.GenerateHead(length) ?? new ReadOnlySequence<byte>();
    }

    public ReadOnlySequence<byte> EncodeBlock(in ReadOnlySequence<byte> input)
    {
        return _encoder?.EncodeBlock(input) ?? input;
    }

    public ReadOnlySequence<byte> EncodeTail()
    {
        return _encoder?.EncodeTail() ?? new ReadOnlySequence<byte>();
    }

    public bool Decode(ReadOnlySequence<byte> bytes, out ReadOnlySequence<byte> frame, out bool isStream, out bool requiresQuickAck,
        out SequencePosition position)
    {
        if (_decoder != null)
        {
            return _decoder.Decode(bytes, out frame, out isStream, out requiresQuickAck, out position);
        }

        frame = new ReadOnlySequence<byte>();
        isStream = false;
        requiresQuickAck = false;
        position = bytes.Start;
        return false;
    }
}