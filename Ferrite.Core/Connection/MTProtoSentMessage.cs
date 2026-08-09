// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Core.Connection;

public static class MTProtoMessageStatus
{
    public const int NothingKnown = 1;
    public const int NotReceived = 2;
    public const int ReceivedNotProcessed = 3;
    public const int ProcessedAwaitingAcknowledgment = 4;
    public const int Stored = 8;
    public const int Sent = 16;
    public const int Ignored = 32;

    public static int ForSentMessage(bool contentRelated)
    {
        return Sent | (contentRelated ? ProcessedAwaitingAcknowledgment : Ignored);
    }

    public static int Acknowledged(int status)
    {
        return status | Stored;
    }
}

public readonly record struct MTProtoSentMessage(
    long MessageId,
    int Status,
    int SequenceNo,
    int Length,
    bool ContentRelated,
    long ResponseToMessageId)
{
    public MTProtoSentMessage(long messageId, int status, int sequenceNo, int length,
        bool contentRelated)
        : this(messageId, status, sequenceNo, length, contentRelated, 0)
    {
    }
}
