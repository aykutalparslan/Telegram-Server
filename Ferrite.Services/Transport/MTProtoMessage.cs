// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Transport;

public class MTProtoMessage
{
    public long SessionId { get; set; }
    public bool IsResponse { get; set; }
    public bool IsContentRelated { get; set; }
    public byte[]? Data { get; set; }
    public MTProtoMessageType MessageType { get; set; }
    public byte[]? Nonce { get; set; }
    public long MessageId { get; set; }
    public int QuickAck { get; set; }
    public long? RecipientUserId { get; set; }
    public int? Pts { get; set; }
}
