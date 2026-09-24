// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Net;
using System.Threading.Channels;
using DotNext.Buffers;
using DotNext.IO;
using DotNext.IO.Pipelines;
using Ferrite.Core.RequestChain;
using Ferrite.Core.Execution.Functions;
using Ferrite.Services.Sessions;
using Ferrite.Core.Execution;
using Ferrite.Services.Common;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.upload;
using Ferrite.TL.Schema;
using Ferrite.Transport;
using Ferrite.Utils;
using Channel = System.Threading.Channels.Channel;

namespace Ferrite.Core.Connection;

public sealed class MTProtoConnection : IMTProtoConnection, IMTProtoSessionOwner
{
    internal event Action<MTProtoConnection>? Stopped;

    public bool IsEncrypted => _session.AuthKeyId != 0;
    public IMTProtoSession Session => _session;
    private readonly ILogger _log;
    private readonly ISessionService _sessionManager;
    private readonly IMTProtoSession _session;
    private readonly ITLHandler _requestChain;
    private readonly IProtoHandler _protoHandler;
    private readonly ITransportConnection _socketConnection;
    private readonly ProtoTransport _protoTransport;
    private readonly Channel<MTProtoMessage> _outgoing = Channel.CreateUnbounded<MTProtoMessage>();
    private readonly Channel<IFileOwner> _outgoingStreams = Channel.CreateUnbounded<IFileOwner>();
    private readonly ILayerTransformRegistry _layerTransforms;
    private readonly SemaphoreSlim _sendSemaphore = new SemaphoreSlim(1, 1);
    private readonly SemaphoreSlim _incomingSemaphore = new SemaphoreSlim(1, 1);
    private Task? _receiveTask;
    private Task? _sendTask;
    private Task? _sendStreamTask;
    private Timer? _disconnectTimer;
    private readonly object _disconnectTimerState = new object();
    private readonly object _abortLock = new object();
    private Task? _stopTask;
    private volatile bool _stopping;
    private const int BadMessageIdTooLow = 16;
    private const int BadServerSaltErrorCode = 48;

    public MTProtoConnection(ITransportConnection connection,
        ILogger logger, ISessionService sessionManager,
        IProtoHandler protoHandler, IMTProtoSession session,
        ProtoTransport protoTransport, ITLHandler requestChain,
        ILayerTransformRegistry? layerTransforms = null)
    {
        _socketConnection = connection;
        _log = logger;
        _sessionManager = sessionManager;
        _session = session;
        _session.Connection = this;
        _session.EndPoint = _socketConnection.RemoteEndPoint as IPEndPoint;
        _protoHandler = protoHandler;
        _protoHandler.Session = _session;
        _protoTransport = protoTransport;
        _requestChain = requestChain;
        _layerTransforms = layerTransforms ??
                           LayerTransformRegistry.Identity(SupportedLayers.Base);
    }
    public void Start()
    {
        _receiveTask = DoReceive();
        _sendTask = DoSend();
        _sendStreamTask = DoSendStreams();
        DelayDisconnect();
    }
    public ValueTask SendAsync(IFileOwner? message)
    {
        if (message != null)
        {
            _outgoingStreams.Writer.TryWrite(message);
        }
        return ValueTask.CompletedTask;
    }
    public ValueTask SendAsync(Services.Transport.MTProtoMessage message)
    {
        if (TryShapeMessage(message, out Services.Transport.MTProtoMessage? shaped))
        {
            _session.TrackOutgoing(message, shaped);
            _outgoing.Writer.TryWrite(shaped);
        }
        return ValueTask.CompletedTask;
    }
    private bool TryShapeMessage(Services.Transport.MTProtoMessage message,
        out Services.Transport.MTProtoMessage shaped)
    {
        shaped = message;
        if (message.Data == null || !message.IsContentRelated ||
            message.MessageType is not (MTProtoMessageType.Encrypted or MTProtoMessageType.Updates) ||
            _session.ConnectionLayer.Value is not ConnectionLayerResolution.Resolved resolved ||
            resolved.Layer == _layerTransforms.BaseLayer)
        {
            return true;
        }

        byte[] source = message.Data;
        int? sourceConstructor = ReadConstructor(source);
        bool isRpcResult = message.IsResponse &&
                           sourceConstructor == Constructors.mtproto_RpcResult;
        if (!isRpcResult && sourceConstructor is { } topConstructor &&
            MTProtoConstructors.All.Contains(topConstructor))
        {
            return true;
        }
        long? rpcRequestMessageId = null;
        if (isRpcResult)
        {
            if (source.Length < 12)
            {
                return HandleTransformFailure(message, resolved.Layer, null,
                    Constructors.mtproto_RpcResult, "rpc_result is truncated.",
                    out shaped);
            }
            rpcRequestMessageId = BinaryPrimitives.ReadInt64LittleEndian(source.AsSpan(4));
            if (ReadConstructor(source, 12) is { } resultConstructor &&
                MTProtoConstructors.All.Contains(resultConstructor))
            {
                return true;
            }
        }

        if (!_layerTransforms.TryCreatePlan(resolved.Layer,
                out LayerTransformPlan plan, out string? planError))
        {
            return HandleTransformFailure(message, resolved.Layer, null,
                ReadConstructor(message.Data), planError ?? "Transform plan is unavailable.",
                out shaped, rpcRequestMessageId);
        }

        if (isRpcResult)
        {
            byte[] resultBytes = source.AsSpan(12).ToArray();
            string? vectorElementType = null;
            if (message.RequestConstructor is { } requestConstructor &&
                plan.TryGetVectorResultElement(requestConstructor, out string element))
            {
                vectorElementType = element;
            }
            LayerTransformResult result = plan.Transform(resultBytes, vectorElementType);
            if (!result.IsSuccess)
            {
                return HandleTransformFailure(message, resolved.Layer,
                    result.FailureEdge, result.Constructor,
                    result.Detail ?? "Layer transform failed.", out shaped,
                    rpcRequestMessageId);
            }
            if (result.Status == LayerTransformStatus.Identity)
            {
                return true;
            }
            byte[] wrapped = new byte[12 + result.Data!.Length];
            source.AsSpan(0, 12).CopyTo(wrapped);
            result.Data.CopyTo(wrapped, 12);
            shaped = CloneWithData(message, wrapped);
            return true;
        }

        LayerTransformResult direct = plan.Transform(source);
        if (!direct.IsSuccess)
        {
            return HandleTransformFailure(message, resolved.Layer,
                direct.FailureEdge, direct.Constructor,
                direct.Detail ?? "Layer transform failed.", out shaped);
        }
        if (direct.Status == LayerTransformStatus.Transformed)
        {
            shaped = CloneWithData(message, direct.Data!);
        }
        return true;
    }

    private bool HandleTransformFailure(Services.Transport.MTProtoMessage message,
        int targetLayer, LayerEdge? edge, int? constructor, string detail,
        out Services.Transport.MTProtoMessage shaped,
        long? requestMessageId = null)
    {
        string edgeText = edge?.ToString() ?? _layerTransforms.BaseLayer + "→" + targetLayer;
        _log.Error("Layer transform failed target:" + targetLayer +
                   " edge:" + edgeText +
                   " constructor:" + FormatConstructor(constructor) +
                   " method:" + FormatConstructor(message.RequestConstructor) +
                   " detail:" + detail);
        if (!message.IsResponse)
        {
            shaped = message;
            return false;
        }

        using TLBytes error = RpcErrorGenerator.GenerateError(
            500, "LAYER_TRANSFORM_FAILED"u8);
        using TLBytes rpcResult = RpcResultGenerator.Generate(error,
            requestMessageId ?? message.MessageId);
        shaped = CloneWithData(message, rpcResult.AsSpan().ToArray());
        return true;
    }

    private static string FormatConstructor(int? constructor)
    {
        return constructor is { } value
            ? "0x" + unchecked((uint)value).ToString("x8")
            : "unknown";
    }

    private static int? ReadConstructor(byte[] data, int offset = 0) =>
        data.Length < offset + 4 ? null : BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));

    private static Services.Transport.MTProtoMessage CloneWithData(
        Services.Transport.MTProtoMessage source, byte[] data)
    {
        return new Services.Transport.MTProtoMessage
        {
            SessionId = source.SessionId,
            IsResponse = source.IsResponse,
            IsContentRelated = source.IsContentRelated,
            Data = data,
            MessageType = source.MessageType,
            Nonce = source.Nonce,
            MessageId = source.MessageId,
            QuickAck = source.QuickAck,
            RecipientUserId = source.RecipientUserId,
            Pts = source.Pts
        };
    }
    private async Task DoReceive()
    {
        while (true)
        {
            await _incomingSemaphore.WaitAsync();
            var result = await _socketConnection.Transport.Input.ReadAsync();
            try
            {
                if (result.Buffer.Length > 0)
                {
                    if (_protoTransport.WebSocketHandshakeCompleted)
                    {
                        var position = await _protoTransport.DecodeWebSocketData(result.Buffer);
                        _socketConnection.Transport.Input.AdvanceTo(position);
                        
                        var wsResult = await _protoTransport.ReadFromWebSocketAsync();
                        var wsPosition = await Process(wsResult);
                        _protoTransport.AdvanceWebSocketTo(wsPosition);
                    }
                    else
                    {
                        var position = await Process(result.Buffer);
                        _socketConnection.Transport.Input.AdvanceTo(position);
                    }
                }
                else
                {
                    _socketConnection.Transport.Input.AdvanceTo(result.Buffer.Start,
                        result.Buffer.End);
                }

                if (result.IsCompleted ||
                    result.IsCanceled)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                if (_stopping)
                {
                    break;
                }
                _log.Error(ex, ex.Message);
            }

            _incomingSemaphore.Release();
        }
    }
    private async Task DoSend()
    {
        while (await _outgoing.Reader.WaitToReadAsync())
        {
            try
            {
                var msg = await _outgoing.Reader.ReadAsync();
                await _sendSemaphore.WaitAsync();
                if (msg.MessageType == MTProtoMessageType.Updates)
                {
                    var outgoingMessage = _protoHandler.EncryptMessage(msg);
                    WriteFrame(outgoingMessage);
                }
                else if (msg.MessageType == MTProtoMessageType.QuickAck)
                {
                    var quickAck = _protoTransport.GenerateQuickAck(msg.QuickAck,
                        _protoTransport.TransportType);
                    WriteQuickAck(quickAck);
                }
                else if (_session.AuthKeyId == 0)
                {
                    var outgoingMessage = _protoHandler.PreparePlaintextMessage(msg);
                    WriteFrame(outgoingMessage);
                }
                else if (_session.AuthKey != null &&
                         _session.AuthKey.Length == 192)
                {
                    var outgoingMessage = _protoHandler.EncryptMessage(msg);
                    WriteFrame(outgoingMessage);
                }

                var result = await FlushSocketAsync();
                if (result.IsCompleted ||
                    result.IsCanceled)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                if (_stopping)
                {
                    break;
                }
                _log.Error(ex, ex.Message);
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }
    }
    private async Task DoSendStreams()
    {
        while (await _outgoingStreams.Reader.WaitToReadAsync())
        {
            try
            {
                var msg = await _outgoingStreams.Reader.ReadAsync();
                await _sendSemaphore.WaitAsync();
                _log.Debug($"=>Sending stream.");

                var (frameLength, frameHeader, outgoingPipe) = 
                    await _protoHandler.GenerateOutgoingStream(msg);
                
                await WriteOutgoingStream(frameLength, frameHeader, outgoingPipe);

                var result = await _socketConnection.FlushAsync();
                if (result.IsCompleted ||
                    result.IsCanceled)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                if (_stopping)
                {
                    break;
                }
                _log.Error(ex, ex.Message);
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }
    }

    private async Task WriteOutgoingStream(int frameLength, ReadOnlySequence<byte> frameHeader, MTProtoPipe outgoingPipe)
    {
        WriteFrameHeader(frameLength);
        WriteFrameBlock(frameHeader);
        await FlushSocketAsync();
        while (true)
        {
            var pipeResult = await outgoingPipe.Input.ReadAsync();
            WriteFrameBlock(pipeResult.Buffer);
            await FlushSocketAsync();
            outgoingPipe.Input.AdvanceTo(pipeResult.Buffer.End);
            if (pipeResult.IsCanceled ||
                pipeResult.IsCompleted)
            {
                break;
            }
        }
        WriteFrameTail();
        await FlushSocketAsync();
    }

    private async ValueTask<SequencePosition> Process(ReadOnlySequence<byte> buffer)
    {
        if (buffer.Length < 4) return buffer.Start;
        SequencePosition position = buffer.Start;
        if (_protoTransport.TransportType == MTProtoTransport.Unknown)
        {
            var rd = new SequenceReader(buffer);
            int firstInt = rd.ReadInt32(true);
            if (firstInt == WebSocketHandler.Get)
            {
                return await ProcessWebSocketHandshake(buffer);
            }

            _protoTransport.DetectTransport(buffer, out position);
        }

        bool hasMore;
        do
        {
            hasMore = _protoTransport.Decode(buffer.Slice(position), out var frame, 
                out var isStream, out var requiresQuickAck, out position);
            try
            {
                if(frame.Length == 0) continue;
                await ProcessIncomingData(frame, isStream, hasMore, requiresQuickAck);
            }
            catch(Exception ex)
            {
                _log.Debug(ex, ex.Message);
            }
        } while (hasMore);

        return position;
    }

    private async ValueTask<SequencePosition> ProcessWebSocketHandshake(ReadOnlySequence<byte> buffer)
    {
        var handshake = _protoTransport.ProcessWebSocketHandshake(buffer);
        if (handshake.Completed)
        {
            WriteFrame(handshake.Response, false);
            await FlushSocketAsync();
        }

        return handshake.Position;
    }

    private async ValueTask ProcessIncomingData(ReadOnlySequence<byte> frame, bool isStream, bool hasMore, bool requiresQuickAck)
    {
        var reader = new SequenceReader(frame);
        long authKeyId = reader.ReadInt64(true);
        if (authKeyId != 0)
        {
            if (!_session.TryFetchAuthKey(authKeyId) &&
                _session.AuthKeyId == 0)
            {
                await SendTransportError(404);
                return;
            }
        }

        if (isStream)
        {
            await ProcessStreamAsync(frame, hasMore);
        }
        else if (frame.Length > 0)
        {
            await ProcessFrameAsync(frame, requiresQuickAck);
        }
    }

    private async ValueTask ProcessStreamAsync(ReadOnlySequence<byte> frame, bool hasMore)
    {
        var message = await _protoHandler.ProcessIncomingStreamAsync(frame, hasMore);
        if (message == StreamingProtoMessage.Default)
        {
            return;
        }

        await CreateNewSession(message.Headers,
            _session.IsKnownSession(message.Headers.SessionId), message.Headers.MessageId);
        var context = GenerateExecutionContext(message.Headers);
        _ = await message.MessageData.Input.ReadInt32Async(true);
        int constructor = await message.MessageData.Input.ReadInt32Async(true);
        if (constructor == Constructors.baseLayer_SaveFilePart)
        {
            var request = await SaveFilePart.ReadAsync(message.MessageData.Input);
            await _requestChain.Process(this, request, context);
        }
        else if (constructor == Constructors.baseLayer_SaveBigFilePart)
        {
            var request = await SaveBigFilePart.ReadAsync(message.MessageData.Input);
            await _requestChain.Process(this, request, context);
        }
    }
    private async ValueTask ProcessFrameAsync(ReadOnlySequence<byte> bytes, bool requiresQuickAck)
    {
        if (bytes.Length < 8)
        {
            return;
        }
        if (_session.PermAuthKeyId != 0 && _session.SessionId != 0)
        {
            _session.SaveCurrentSession(_session.PermAuthKeyId);
        }
        if (_session.AuthKeyId == 0)
        {
            using var message = _protoHandler.ReadPlaintextMessage(bytes.Slice(8));
            var context = GenerateExecutionContext(message.Headers);
            await _requestChain.Process(this, message.MessageData, context);
        }
        else if(_session.AuthKey != null)
        {
            var message = _protoHandler.DecryptMessage(bytes.Slice(8));
            var validServerSalt = _session.IsValidServerSalt(
                message.Headers.Salt, out var currentServerSalt);
            if (!validServerSalt)
            {
                await SendBadServerSalt(message.Headers, currentServerSalt);
                message.Dispose();
                return;
            }

            bool isContainer = message.MessageData.Constructor ==
                               Constructors.mtproto_MsgContainer;
            bool knownSession = _session.IsKnownSession(message.Headers.SessionId);
            if (!_session.TryValidateMessageId(message.Headers.SessionId,
                    message.Headers.MessageId, out var errorCode, isContainer))
            {
                await SendBadMsgNotification(message.Headers, errorCode);
                message.Dispose();
                return;
            }

            bool bound = await CreateNewSession(message.Headers, knownSession, isContainer
                ? FirstContainedMessageId(message.MessageData, message.Headers.MessageId)
                : message.Headers.MessageId);
            var context = GenerateExecutionContext(message.Headers,
                requiresQuickAck ? _session.GenerateQuickAck(message.MessageData.AsSpan()) : null);
            await _requestChain.Process(this, message.MessageData, context);
            if (bound)
            {
                foreach (var pending in _session.TakeUnacknowledged())
                {
                    await SendAsync(pending);
                }
            }
        }
    }

    private async ValueTask<bool> CreateNewSession(ProtoHeaders headers, bool knownSession,
        long firstMessageId)
    {
        if (_session.SessionId == headers.SessionId) return false;
        bool firstOnConnection = _session.SessionId == 0;
        if (!firstOnConnection)
        {
            await _sessionManager.RemoveSession(_session.AuthKeyId,
                _session.PermAuthKeyId, _session.SessionId, this);
        }
        var serverSalt = _session.CreateNewSession(headers.SessionId, headers.MessageId);
        if (firstOnConnection && !knownSession)
        {
            await SendNewSessionCreatedMessage(firstMessageId, serverSalt);
        }
        return true;
    }

    private static long FirstContainedMessageId(TLBytes container, long containerId)
    {
        var messages = new TL.mtproto.MsgContainer(container.AsSpan()).Messages;
        long first = containerId;
        for (int i = 0; i < messages.Count; i++)
        {
            first = Math.Min(first, BitConverter.ToInt64(
                messages.Read(TL.mtproto.MessageBare.Read)));
        }
        return first;
    }

    private TLExecutionContext GenerateExecutionContext(ProtoHeaders headers, int? quickAck = null)
    {
        _session.TryResolvePermAuthKeyId();
        var context = new TLExecutionContext(_session.SessionData)
        {
            ConnectionLayer = _session.ConnectionLayer,
            AuthKeyId = _session.AuthKeyId,
            PermAuthKeyId = _session.PermAuthKeyId,
            Salt = headers.Salt,
            MessageId = headers.MessageId,
            SequenceNo = headers.SequenceNo,
            SessionId = headers.SessionId,
        };
        if (_socketConnection.RemoteEndPoint is IPEndPoint endPoint)
        {
            context.IP = endPoint.Address.ToString();
        }
        if (quickAck != null)
        {
            context.QuickAck = quickAck;
        }

        return context;
    }
    private async ValueTask SendNewSessionCreatedMessage(long firstMessageId, long serverSalt)
    {
        var sessionCreated = _session.GenerateSessionCreated(firstMessageId, serverSalt);
        await SendAsync(sessionCreated);
    }
    private async ValueTask SendBadMsgNotification(ProtoHeaders headers, int errorCode)
    {
        byte[] payload;
        using (var notification = Ferrite.TL.mtproto.BadMsgNotification.Builder()
                   .BadMsgId(headers.MessageId)
                   .BadMsgSeqno(headers.SequenceNo)
                   .ErrorCode(errorCode == 0 ? BadMessageIdTooLow : errorCode)
                   .Build())
        {
            payload = notification.TLBytes!.Value.AsSpan().ToArray();
        }

        await SendAsync(new Services.Transport.MTProtoMessage
        {
            Data = payload,
            IsContentRelated = false,
            IsResponse = true,
            MessageType = MTProtoMessageType.Encrypted,
            SessionId = headers.SessionId,
            MessageId = headers.MessageId
        });
    }
    private async ValueTask SendBadServerSalt(ProtoHeaders headers, long currentServerSalt)
    {
        byte[] payload;
        using (var notification = Ferrite.TL.mtproto.BadServerSalt.Builder()
                   .BadMsgId(headers.MessageId)
                   .BadMsgSeqno(headers.SequenceNo)
                   .ErrorCode(BadServerSaltErrorCode)
                   .NewServerSalt(currentServerSalt)
                   .Build())
        {
            payload = notification.TLBytes!.Value.AsSpan().ToArray();
        }

        await SendAsync(new Services.Transport.MTProtoMessage
        {
            Data = payload,
            IsContentRelated = false,
            IsResponse = true,
            MessageType = MTProtoMessageType.Encrypted,
            SessionId = headers.SessionId,
            MessageId = headers.MessageId
        });
    }
    private async ValueTask SendTransportError(int errorCode)
    {
        var transportError = _protoTransport.GenerateTransportError(errorCode);
        WriteFrame(transportError);
        await FlushSocketAsync();
    }
    public async ValueTask Ping(long pingId, long requestMessageId, int delayDisconnectInSeconds = 0)
    {
        if (delayDisconnectInSeconds > 0)
        {
            DelayDisconnect(delayDisconnectInSeconds * 1000);
        }

        long authKeyId = _session.PermAuthKeyId != 0
            ? _session.PermAuthKeyId
            : _session.AuthKeyId;
        await _sessionManager.OnPing(authKeyId, _session.SessionId);
        byte[] payload;
        {
            using var pong = Ferrite.TL.mtproto.Pong.Builder()
                .MsgId(requestMessageId)
                .PingId(pingId)
                .Build();
            payload = pong.TLBytes!.Value.AsSpan().ToArray();
        }

        Services.Transport.MTProtoMessage message = new Services.Transport.MTProtoMessage()
        {
            Data = payload,
            IsContentRelated = false,
            IsResponse = true,
            MessageType = MTProtoMessageType.Pong,
            SessionId = _session.SessionId,
            MessageId = requestMessageId
        };
        await SendAsync(message);
    }
    private void WriteFrame(ReadOnlySequence<byte> buffer, bool webSocketFeatureEnabled = true)
    {
        if(buffer.Length == 0) return;
        var encoded = _protoTransport.Encode(buffer);
        if (webSocketFeatureEnabled &&
            _protoTransport.WebSocketHandshakeCompleted)
        {
            var webSocketHeader = _protoTransport.GenerateWebSocketHeader((int)encoded.Length);
            _socketConnection.Write(webSocketHeader);
        }
        _socketConnection.Write(encoded);
    }
    internal void WriteFrameHeader(int length)
    {
        if(length == 0 || _protoTransport.TransportType == MTProtoTransport.Unknown) return;
        var header = _protoTransport.GenerateHead(length);
        
        if (_protoTransport.WebSocketHandshakeCompleted)
        {
            var webSocketHeader = _protoTransport.GenerateWebSocketHeader((int)header.Length + length);
            _socketConnection.Write(webSocketHeader);
        }

        WriteFrameBlock(header);
    }
    private void WriteQuickAck(ReadOnlySequence<byte> quickAck)
    {
        if (quickAck.Length == 0) return;
        if (_protoTransport.WebSocketHandshakeCompleted)
        {
            _socketConnection.Write(
                _protoTransport.GenerateWebSocketHeader((int)quickAck.Length));
        }
        WriteFrameBlock(quickAck);
    }
    internal void WriteFrameBlock(ReadOnlySequence<byte> buffer)
    {
        if(buffer.Length == 0) return;
        var encoded = _protoTransport.EncodeBlock(buffer);
        _socketConnection.Write(encoded);
    }
    internal void WriteFrameTail()
    {
        if (_protoTransport.TransportType == MTProtoTransport.Unknown) return;
        var frameTail = _protoTransport.EncodeTail();
        if (frameTail.Length > 0)
        {
            _socketConnection.Transport.Output.Write(frameTail);
        }
    }
    internal ValueTask<FlushResult> FlushSocketAsync()
    {
        return _socketConnection.FlushAsync();
    }
    private void DelayDisconnect(int delayInMilliseconds = 750000)
    {
        lock (_disconnectTimerState)
        {
            if (_disconnectTimer == null)
            {
                _disconnectTimer = new Timer((state) =>
                {
                    Abort(new Exception());
                }, _disconnectTimerState, delayInMilliseconds, delayInMilliseconds);
            }
            else
            {
                _disconnectTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _disconnectTimer.Change(delayInMilliseconds, delayInMilliseconds);
            }
        }
    }
    public void Abort(Exception abortReason)
    {
        _ = StopAsync(abortReason);
    }

    public ValueTask StopAsync(Exception abortReason)
    {
        lock (_abortLock)
        {
            _stopTask ??= StopCoreAsync(abortReason);
            return new ValueTask(_stopTask);
        }
    }

    private async Task StopCoreAsync(Exception abortReason)
    {
        _stopping = true;
        Exception? stopError = null;
        try
        {
            await _sessionManager.RemoveSession(_session.AuthKeyId,
                _session.PermAuthKeyId, _session.SessionId, this);
        }
        catch (Exception ex)
        {
            stopError = ex;
        }

        _outgoing.Writer.TryComplete();
        _outgoingStreams.Writer.TryComplete();
        _disconnectTimer?.Dispose();
        try
        {
            _socketConnection.Abort(abortReason);
        }
        catch (Exception ex)
        {
            stopError ??= ex;
        }
        try
        {
            await _socketConnection.DisposeAsync();
        }
        catch (Exception ex)
        {
            stopError ??= ex;
        }
        try
        {
            Task[] workers = new[] { _receiveTask, _sendTask, _sendStreamTask }
                .Where(task => task is not null).Cast<Task>().ToArray();
            if (workers.Length > 0)
            {
                await Task.WhenAll(workers);
            }
        }
        catch (Exception ex)
        {
            stopError ??= ex;
        }
        finally
        {
            if (stopError is not null)
            {
                _log.Verbose(stopError,
                    $"Connection closed for authKeyId{_session.AuthKeyId}");
            }
            Stopped?.Invoke(this);
        }
    }
}
