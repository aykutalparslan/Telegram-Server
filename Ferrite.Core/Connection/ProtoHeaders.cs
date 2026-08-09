// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Core.Connection;

public readonly struct ProtoHeaders
{
    public ProtoHeaders(long authKeyId, long salt, long sessionId, long messageId, int sequenceNo)
    {
        AuthKeyId = authKeyId;
        Salt = salt;
        SessionId = sessionId;
        MessageId = messageId;
        SequenceNo = sequenceNo;
    }

    public long AuthKeyId { get; init; }
    public long Salt { get; init; }
    public long SessionId { get; init; }
    public long MessageId { get; init; }
    public int SequenceNo { get; init; }
}