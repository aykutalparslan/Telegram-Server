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
    int GenerateQuickAck(Span<byte> messageSpan);
    int GenerateSeqNo(bool isContentRelated);
    void RecordSentMessage(long messageId, int sequenceNo, int length, bool contentRelated);
    void RecordSentMessage(long messageId, int sequenceNo, int length, bool contentRelated,
        long responseToMessageId);
    bool TryGetSentMessage(long messageId, out MTProtoSentMessage message);
    bool MarkSentMessageAcknowledged(long messageId);

    /// <summary>
    /// Gets the next Message Identifier (msg_id) for this session.
    /// </summary>
    /// <param name="response">If the message is a response to a client message.</param>
    /// <returns></returns>
    long NextMessageId(bool response);

    long CreateNewSession(long sessionId, long firstMessageId);

    /// <summary>
    /// Checks if the given message Id is valid and adds it to the last N messages list
    /// </summary>
    /// <param name="messageId"></param>
    /// <returns></returns>
    bool IsValidMessageId(long messageId);
    bool TryValidateMessageId(long messageId, out int errorCode,
        bool isContainer = false);
    bool IsValidServerSalt(long serverSalt, out long currentServerSalt);

    Services.MTProtoMessage GenerateSessionCreated(long firstMessageId, long serverSalt);

    public long SaveCurrentSession(long authKeyId);
}
