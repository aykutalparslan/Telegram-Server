// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Buffers;
using Ferrite.Core.Connection;
using Ferrite.Core.Execution;
using Ferrite.TL;

namespace Ferrite.Core.RequestChain;

public class ServiceMessagesProcessor : ILinkedHandler
{
    public ILinkedHandler SetNext(ILinkedHandler value)
    {
        Next = value;
        return Next;
    }

    public ILinkedHandler? Next { get; set; }

    public async ValueTask Process(object? sender, TLBytes input, TLExecutionContext ctx)
    {
        if (sender is IMTProtoConnection connection)
        {
            if (ctx.QuickAck != null)
            {
                Services.Transport.MTProtoMessage message = new Services.Transport.MTProtoMessage()
                {
                    QuickAck = (int)ctx.QuickAck,
                    MessageType = MTProtoMessageType.QuickAck,
                    SessionId = ctx.SessionId,
                    MessageId = ctx.MessageId
                };
                await connection.SendAsync(message);
            }

            if (TryReadPing(input, out var pingId, out var disconnectDelay))
            {
                await connection.Ping(pingId, ctx.MessageId, disconnectDelay);
                input.Dispose();
                return;
            }
        }

        if (input.Constructor == Constructors.mtproto_MsgsAck)
        {
            if (sender is IMTProtoSessionOwner sessionOwner)
            {
                var ack = new TL.mtproto.MsgsAck(input.AsSpan());
                var msgIds = ack.MsgIds;
                for (var i = 0; i < msgIds.Count; i++)
                {
                    sessionOwner.Session.MarkSentMessageAcknowledged(msgIds[i]);
                }
            }

            input.Dispose();
            return;
        }

        if (input.Constructor == Constructors.mtproto_RpcDropAnswer)
        {
            if (sender is IMTProtoConnection dropConnection &&
                sender is IMTProtoSessionOwner dropOwner)
            {
                var dropAnswer = new TL.mtproto.RpcDropAnswer(input.AsSpan());
                await SendRpcDropAnswer(dropConnection, dropOwner.Session,
                    dropAnswer.ReqMsgId, ctx.MessageId, ctx.SessionId);
            }

            input.Dispose();
            return;
        }

        if (input.Constructor == Constructors.mtproto_MsgsStateReq)
        {
            if (sender is IMTProtoConnection stateConnection &&
                sender is IMTProtoSessionOwner stateOwner)
            {
                var stateReq = new TL.mtproto.MsgsStateReq(input.AsSpan());
                byte[] info = BuildStateInfoBytes(stateOwner.Session, stateReq.MsgIds);
                await SendMsgsStateInfo(stateConnection, ctx.MessageId, info, ctx.SessionId);
            }

            input.Dispose();
            return;
        }

        if (input.Constructor == Constructors.mtproto_MsgResendReq)
        {
            if (sender is IMTProtoConnection resendConnection &&
                sender is IMTProtoSessionOwner resendOwner)
            {
                var resendReq = new TL.mtproto.MsgResendReq(input.AsSpan());
                byte[] info = BuildStateInfoBytes(resendOwner.Session, resendReq.MsgIds);
                await SendMsgsStateInfo(resendConnection, ctx.MessageId, info, ctx.SessionId);
            }

            input.Dispose();
            return;
        }

        if (input.Constructor == Constructors.mtproto_MsgsAllInfo)
        {
            if (sender is IMTProtoSessionOwner allInfoOwner)
            {
                var allInfo = new TL.mtproto.MsgsAllInfo(input.AsSpan());
                AcknowledgeReceivedMessages(allInfoOwner.Session, allInfo.MsgIds, allInfo.Info);
            }

            input.Dispose();
            return;
        }

        if (input.Constructor == Constructors.mtproto_MsgsStateInfo)
        {
            input.Dispose();
            return;
        }

        if (input.Constructor == Constructors.mtproto_HttpWait)
        {
            input.Dispose();
            return;
        }

        if (Next != null) await Next.Process(sender, input, ctx);
        else input.Dispose();
    }

    private const byte MessageStatusNothingKnown = 1;
    private const byte MessageStatusReceivedAndProcessed = 4;
    private const byte MessageStatusAlreadyAcknowledged = 8;
    private const byte MessageStatusNotRequiringAck = 16;
    private const byte MessageStatusBaseMask = 0x07;

    private static byte[] BuildStateInfoBytes(IMTProtoSession session, TL.VectorOfLong msgIds)
    {
        int count = msgIds.Count;
        var info = new byte[count];
        for (int i = 0; i < count; i++)
        {
            info[i] = ComputeStateInfoByte(session, msgIds[i]);
        }

        return info;
    }

    private static byte ComputeStateInfoByte(IMTProtoSession session, long msgId)
    {
        if (!session.TryGetSentMessage(msgId, out var sent))
        {
            return MessageStatusNothingKnown;
        }

        int status = MessageStatusReceivedAndProcessed;
        if (!sent.ContentRelated)
        {
            status |= MessageStatusNotRequiringAck;
        }

        if ((sent.Status & MTProtoMessageStatus.Stored) != 0)
        {
            status |= MessageStatusAlreadyAcknowledged;
        }

        return (byte)status;
    }

    private static async ValueTask SendMsgsStateInfo(IMTProtoConnection connection,
        long reqMsgId, byte[] info, long sessionId)
    {
        byte[] payload;
        using (var stateInfo = TL.mtproto.MsgsStateInfo.Builder()
                   .ReqMsgId(reqMsgId)
                   .Info(info)
                   .Build())
        {
            payload = stateInfo.TLBytes!.Value.AsSpan().ToArray();
        }

        await SendServiceMessage(connection, payload, reqMsgId, sessionId);
    }

    private static async ValueTask SendServiceMessage(IMTProtoConnection connection,
        byte[] payload, long responseToMessageId, long sessionId)
    {
        var message = new Services.Transport.MTProtoMessage
        {
            Data = payload,
            IsContentRelated = false,
            IsResponse = true,
            MessageType = MTProtoMessageType.Encrypted,
            SessionId = sessionId,
            MessageId = responseToMessageId
        };
        await connection.SendAsync(message);
    }

    private static void AcknowledgeReceivedMessages(IMTProtoSession session,
        TL.VectorOfLong msgIds, ReadOnlySpan<byte> info)
    {
        int count = Math.Min(msgIds.Count, info.Length);
        for (int i = 0; i < count; i++)
        {
            byte status = info[i];
            bool received = (status & MessageStatusBaseMask) == MessageStatusReceivedAndProcessed;
            bool acknowledged = (status & MessageStatusAlreadyAcknowledged) != 0;
            if (received || acknowledged)
            {
                session.MarkSentMessageAcknowledged(msgIds[i]);
            }
        }
    }

    private static async ValueTask SendRpcDropAnswer(IMTProtoConnection connection,
        IMTProtoSession session, long reqMsgId, long responseToMessageId, long sessionId)
    {
        byte[] payload;
        if (session.TryGetSentMessage(reqMsgId, out var sent))
        {
            using var dropped = TL.mtproto.RpcAnswerDropped.Builder()
                .MsgId(sent.MessageId)
                .SeqNo(sent.SequenceNo)
                .Bytes(sent.Length)
                .Build();
            payload = dropped.TLBytes!.Value.AsSpan().ToArray();
        }
        else
        {
            using var unknown = TL.mtproto.RpcAnswerUnknown.Builder().Build();
            payload = unknown.TLBytes!.Value.AsSpan().ToArray();
        }

        await SendServiceMessage(connection, payload, responseToMessageId, sessionId);
    }

    private static bool TryReadPing(TLBytes input, out long pingId, out int disconnectDelay)
    {
        if (input.Constructor == Constructors.mtproto_Ping)
        {
            var ping = new TL.mtproto.Ping(input.AsSpan());
            pingId = ping.PingId;
            disconnectDelay = 0;
            return true;
        }

        if (input.Constructor == Constructors.mtproto_PingDelayDisconnect)
        {
            var pingDelay = new TL.mtproto.PingDelayDisconnect(input.AsSpan());
            pingId = pingDelay.PingId;
            disconnectDelay = pingDelay.DisconnectDelay;
            return true;
        }

        pingId = 0;
        disconnectDelay = 0;
        return false;
    }

    public async ValueTask Process(object? sender, ITLStreamingObject input, TLExecutionContext ctx)
    {
        if (Next != null) await Next.Process(sender, input, ctx);
    }
}
