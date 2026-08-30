// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Net;

namespace Ferrite.Core.Connection;

public interface IMTProtoSession
{
    MTProtoConnection? Connection { get; set; }
    IPEndPoint? EndPoint { get; set; }
    long AuthKeyId { get; }
    long PermAuthKeyId { get; }
    byte[]? AuthKey { get; }
    long SessionId { get; }
    long UniqueSessionId { get; }
    long ServerSalt { get; }
    Dictionary<string, object> SessionData { get; }
    bool TryFetchAuthKey(long authKeyId);

    bool TryResolvePermAuthKeyId();
    int GenerateQuickAck(Span<byte> messageSpan);
    int GenerateSeqNo(bool isContentRelated);
    void RecordSentMessage(long messageId, int sequenceNo, int length, bool contentRelated);
    void RecordSentMessage(long messageId, int sequenceNo, int length, bool contentRelated,
        long responseToMessageId);
    bool TryGetSentMessage(long messageId, out MTProtoSentMessage message);
    bool MarkSentMessageAcknowledged(long messageId);

    long NextMessageId(bool response);

    long CreateNewSession(long sessionId, long firstMessageId);

    bool IsValidMessageId(long sessionId, long messageId);
    bool TryValidateMessageId(long sessionId, long messageId, out int errorCode,
        bool isContainer = false);
    bool IsValidServerSalt(long serverSalt, out long currentServerSalt);

    Services.Transport.MTProtoMessage GenerateSessionCreated(long firstMessageId, long serverSalt);

    public long SaveCurrentSession(long authKeyId);
}
