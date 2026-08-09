// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using System.IO.Pipelines;
using Ferrite.Transport;

namespace Ferrite.Core.Connection.TransportFeatures;

public class WebSocketFeature : IWebSocketFeature
{
    public bool WebSocketHandshakeCompleted { get; private set; }
    private WebSocketHandler _handler;

    public WebSocketHandler? WebSocketHandler => WebSocketHandshakeCompleted ? _handler : null;

    public PipeReader WebSocketReader { get; }
    private readonly Pipe _webSocketPipe;

    public WebSocketFeature()
    {
        _handler = new();
        _webSocketPipe = new Pipe();
        WebSocketReader = _webSocketPipe.Reader;
    }
    public HandshakeResponse ProcessWebSocketHandshake(ReadOnlySequence<byte> data)
    {
        var pos = ParseHeaders(data);
        if (!_handler.HeadersComplete) return new HandshakeResponse(pos, 
            new ReadOnlySequence<byte>(), false);
        var response = _handler.GenerateHandshakeResponse();
        WebSocketHandshakeCompleted = true;
        return new HandshakeResponse(pos, 
            response, true);
    }

    public async ValueTask<SequencePosition> DecodeWebSocketData(ReadOnlySequence<byte> buffer)
    {
        var pos = _handler.DecodeTo(buffer, _webSocketPipe.Writer);
        await _webSocketPipe.Writer.FlushAsync();
        return pos;
    }

    public ReadOnlySequence<byte> GenerateWebSocketHeader(int length)
    {
        if (!WebSocketHandshakeCompleted) return new ReadOnlySequence<byte>();
        var header = WebSocketHandler.GenerateHeader(length);
        return new ReadOnlySequence<byte>(header);
    }

    private SequencePosition ParseHeaders(ReadOnlySequence<byte> data)
    {
        var reader = new SequenceReader<byte>(data);
        HttpParser<WebSocketHandler> parser = new HttpParser<WebSocketHandler>();
        if (!_handler.RequestLineComplete)
        {
            parser.ParseRequestLine(_handler, ref reader);
        }
        parser.ParseHeaders(_handler, ref reader);
        return reader.Position;
    }
}