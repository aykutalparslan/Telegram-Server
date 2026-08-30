// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Collections.Concurrent;
using Ferrite.Core.Execution;
using Ferrite.Core.Execution.Functions;
using Ferrite.Services.Sessions;
using Ferrite.TL;
using Ferrite.Utils;

namespace Ferrite.Core.RequestChain;

public class MTProtoRequestProcessor : ILinkedHandler
{
    private const int AnsweredMessageLimit = 4096;

    private readonly ISessionService _sessionManager;
    private readonly IMessagePipe _pipe;
    private readonly IExecutionEngine _api;
    private readonly ILogger _log;
    private readonly ConcurrentDictionary<(long Session, long MessageId),
        Lazy<Task<byte[]?>>> _answers = new();
    private readonly ConcurrentQueue<(long, long)> _answered = new();
    public MTProtoRequestProcessor(ISessionService sessionManager, IMessagePipe pipe,
        IExecutionEngine api, ILogger log)
    {
        _sessionManager = sessionManager;
        _pipe = pipe;
        _api = api;
        _log = log;
    }
    
    public ILinkedHandler SetNext(ILinkedHandler value)
    {
        Next = value;
        return Next;
    }

    public ILinkedHandler? Next { get; set; }

    public ValueTask Process(object? sender, ITLStreamingObject input, TLExecutionContext ctx)
    {
        if (sender is IMTProtoConnection connection)
        {
            _ = ProcessStreamingAsync(connection, input, ctx);
        }

        return ValueTask.CompletedTask;
    }

    private async Task ProcessStreamingAsync(IMTProtoConnection connection, ITLStreamingObject input,
        TLExecutionContext ctx)
    {
        try
        {
            await ProcessAndSend(connection, ctx, () => _api.Invoke(input, ctx), ack: true,
                input.Constructor);
        }
        catch (Exception e)
        {
            _log.Error(e, $"😭 => {this} => streaming #{input.Constructor:x} => {e.Message}");
        }
        finally
        {
            try
            {
                await input.DrainAsync();
            }
            catch (Exception e)
            {
                _log.Error(e, $"😭 => {this} => drain #{input.Constructor:x} => {e.Message}");
            }
        }
    }

    private static byte[] BuildMsgsAckPayload(long msgId)
    {
        var msgIds = new TL.VectorOfLong();
        msgIds.Append(msgId);
        using var ack = TL.mtproto.MsgsAck.Builder().MsgIds(msgIds).Build();
        return ack.TLBytes!.Value.AsSpan().ToArray();
    }

    public async ValueTask Process(object? sender, TLBytes input, TLExecutionContext ctx)
    {
        if (sender is IMTProtoConnection connection)
        {
            if (input.Constructor == Constructors.baseLayer_GetFile ||
                _api.IsFileRequest(input))
            {
                await ProcessFile(connection, input, ctx);
            }
            else
            {
                await ProcessAndSend(connection, ctx, () => _api.Invoke(input, ctx),
                    RequiresEarlyAck(input.Constructor), input.Constructor);
            }
        }
        else if (await _api.Invoke(input, ctx) is { } result)
        {
            using (result)
            {
                await ForwardToRemoteSession(ctx, result.AsSpan().ToArray());
            }
        }
        if (Next != null) await Next.Process(sender, input, ctx);
        else input.Dispose();
    }

    private async Task ProcessAndSend(IMTProtoConnection connection, TLExecutionContext ctx,
        Func<ValueTask<TLBytes?>> invoke, bool ack, int constructor)
    {
        if (ack) await Send(connection, ctx, BuildMsgsAckPayload(ctx.MessageId));
        if (await AnswerOnce(ctx, invoke, constructor) is { } data)
        {
            await Send(connection, ctx, data);
        }
    }

    private Task<byte[]?> AnswerOnce(TLExecutionContext ctx, Func<ValueTask<TLBytes?>> invoke,
        int constructor)
    {
        _log.Debug($"📥 request #{constructor.ToString("x")} session={ctx.SessionId} " +
                   $"msgId={ctx.MessageId} authKey={ctx.AuthKeyId} " +
                   $"permAuthKey={ctx.PermAuthKeyId}");
        var identity = (ctx.SessionId, ctx.MessageId);
        var answer = _answers.GetOrAdd(identity, _ => new Lazy<Task<byte[]?>>(
            () => InvokeAsync(invoke), LazyThreadSafetyMode.ExecutionAndPublication));
        if (answer.IsValueCreated)
        {
            _log.Debug($"repeated message {ctx.MessageId} in session {ctx.SessionId} " +
                       "is answered from its first execution");
        }
        else
        {
            RememberAnswer(identity);
        }
        return answer.Value;
    }

    private static async Task<byte[]?> InvokeAsync(Func<ValueTask<TLBytes?>> invoke)
    {
        if (await invoke() is not { } result) return null;
        using (result) return result.AsSpan().ToArray();
    }

    private void RememberAnswer((long, long) identity)
    {
        _answered.Enqueue(identity);
        while (_answered.Count > AnsweredMessageLimit && _answered.TryDequeue(out var evicted))
        {
            _answers.TryRemove(evicted, out _);
        }
    }

    private async Task ProcessFile(IMTProtoConnection connection, TLBytes input, TLExecutionContext ctx)
    {
        var result = await _api.InvokeFile(input, ctx);
        if (result.File != null) await connection.SendAsync(result.File);
        else if (result.Error is { } error)
        {
            using (error)
            using (var rpcResult = RpcResultGenerator.Generate(error, ctx.MessageId))
            {
                await Send(connection, ctx, rpcResult.AsSpan().ToArray());
            }
        }
    }

    private static bool RequiresEarlyAck(int constructor)
    {
        return constructor is Constructors.baseLayer_UploadProfilePhoto;
    }

    private static ValueTask Send(IMTProtoConnection connection, TLExecutionContext ctx, byte[] data)
    {
        return connection.SendAsync(BuildResponse(ctx, data));
    }

    private async Task ForwardToRemoteSession(TLExecutionContext ctx, byte[] data)
    {
        if (await _sessionManager.GetSessionStateAsync(ctx.SessionId) is { } session)
        {
            var bytes = MTProtoMessageEnvelope.Serialize(BuildResponse(ctx, data));
            await _pipe.WriteMessageAsync(MessagePipeChannels.ForNode(session.NodeId), bytes);
        }
    }

    private static MTProtoMessage BuildResponse(TLExecutionContext ctx, byte[] data)
    {
        return new MTProtoMessage
        {
            MessageType = MTProtoMessageType.Encrypted,
            SessionId = ctx.SessionId,
            IsResponse = true,
            IsContentRelated = true,
            MessageId = ctx.MessageId,
            Data = data
        };
    }
}
