// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using DotNext.Buffers;
using Ferrite.Data;
using Ferrite.Services;
using Ferrite.Core.Execution;
using Ferrite.TL;
using Ferrite.TL.mtproto;

namespace Ferrite.Core.RequestChain;

public class MsgContainerProcessor : ILinkedHandler
{
    private readonly ISessionService _sessionManager;
    private readonly IMessagePipe _pipe;
    public MsgContainerProcessor(ISessionService sessionManager, IMessagePipe pipe)
    {
        _sessionManager = sessionManager;
        _pipe = pipe;
    }
    
    public ILinkedHandler SetNext(ILinkedHandler value)
    {
        Next = value;
        return Next;
    }

    public ILinkedHandler? Next { get; set; }

    public async ValueTask Process(object? sender, TLBytes input, TLExecutionContext ctx)
    {
        if (input.Constructor == Constructors.mtproto_MsgContainer)
        {
            var messages = GetContainedMessages(input);
            var ackMsgIds = new List<long>(messages.Length + 1) { ctx.MessageId };
            foreach (var message in messages)
            {
                var (msgId, body) = GetMsgIdAndBody(message);
                message.Dispose();
                ackMsgIds.Add(msgId);
                if (Next != null) await Next.Process(sender, body, ctx with{MessageId = msgId});
                else body.Dispose();
            }
            await SendMsgsAck(sender, ctx, ackMsgIds);
            input.Dispose();
        }
        else if (Next != null) await Next.Process(sender, input, ctx);
        else input.Dispose();
    }

    private static ValueTuple<long, TLBytes> GetMsgIdAndBody(TLBytes input)
    {
        var message = new MessageBare(input.AsSpan());
        var memoryOwner = UnmanagedMemoryPool<byte>.Shared.Rent(message.Body.Length);
        message.Body.CopyTo(memoryOwner.Memory.Span);
        var body = new TLBytes(memoryOwner, 0, message.Body.Length);
        return (message.MsgId, body);
    }

    private static TLBytes[] GetContainedMessages(TLBytes input)
    {
        TL.mtproto.MsgContainer container = new(input.AsSpan());
        var messages = new TLBytes[container.Messages.Count];
        var messageVector = container.Messages;
        for (int i = 0; i < messages.Length; i++)
        {
            var message = messageVector.Read(MessageBare.Read);
            var memoryOwner = UnmanagedMemoryPool<byte>.Shared.Rent(message.Length);
            message.CopyTo(memoryOwner.Memory.Span);
            messages[i] = new TLBytes(memoryOwner, 0, message.Length);
        }

        return messages;
    }

    private async ValueTask SendMsgsAck(object? sender, TLExecutionContext ctx, IReadOnlyList<long> msgIds)
    {
        Services.MTProtoMessage message = new Services.MTProtoMessage
        {
            SessionId = ctx.SessionId,
            IsResponse = true,
            IsContentRelated = true,
            Data = BuildMsgsAckPayload(msgIds)
        };

        if(sender is IMTProtoConnection connection)
        {
            await connection.SendAsync(message);
        }
        else if (await _sessionManager.GetSessionStateAsync(ctx.SessionId)
                 is { } session)
        {
            var bytes = MTProtoMessageEnvelope.Serialize(message);
            _ = _pipe.WriteMessageAsync(MessagePipeChannels.ForNode(session.NodeId), bytes);
        }
    }

    private static byte[] BuildMsgsAckPayload(IReadOnlyList<long> msgIds)
    {
        var msgIdVector = new TL.VectorOfLong();
        foreach (var msgId in msgIds)
        {
            msgIdVector.Append(msgId);
        }

        using var ack = TL.mtproto.MsgsAck.Builder().MsgIds(msgIdVector).Build();
        return ack.TLBytes!.Value.AsSpan().ToArray();
    }

    public async ValueTask Process(object? sender, ITLStreamingObject input, TLExecutionContext ctx)
    {
        if (Next != null) await Next.Process(sender, input, ctx);
    }
}
