// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

public static class MTProtoMessageEnvelope
{
    public static byte[] Serialize(MTProtoMessage message)
    {
        var builder = MtprotoMessage.Builder()
            .SessionId(message.SessionId)
            .IsResponse(message.IsResponse)
            .IsContentRelated(message.IsContentRelated)
            .MessageType((int)message.MessageType)
            .MessageId(message.MessageId)
            .QuickAck(message.QuickAck);
        if (message.Data != null) builder.Data(message.Data);
        if (message.Nonce != null) builder.Nonce(message.Nonce);
        using MtprotoMessage row = builder.Build();
        return row.ToReadOnlySpan().ToArray();
    }

    public static MTProtoMessage Deserialize(byte[] bytes)
    {
        if (bytes.Length < sizeof(int))
        {
            throw new InvalidDataException("MTProto pipe envelope is truncated.");
        }
        var tl = new TLBytes(bytes, 0, bytes.Length);
        if (tl.Constructor != Constructors.baseLayer_MtprotoMessage)
        {
            throw new InvalidDataException("MTProto pipe envelope codec/version mismatch.");
        }
        var row = ((TLMTProtoMessage)tl).AsMtprotoMessage();
        if (!Enum.IsDefined(typeof(MTProtoMessageType), row.MessageType))
        {
            throw new InvalidDataException("MTProto pipe envelope has an invalid message type.");
        }
        return new MTProtoMessage
        {
            SessionId = row.SessionId,
            IsResponse = row.IsResponse,
            IsContentRelated = row.IsContentRelated,
            Data = row.Flags[0] ? row.Data.ToArray() : null,
            MessageType = (MTProtoMessageType)row.MessageType,
            Nonce = row.Flags[1] ? row.Nonce.ToArray() : null,
            MessageId = row.MessageId,
            QuickAck = row.QuickAck,
        };
    }
}
