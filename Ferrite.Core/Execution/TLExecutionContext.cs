// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Core.Execution;

public record TLExecutionContext(Dictionary<string, object> SessionData)
{
    public Dictionary<string, object> SessionData { get; set; } = SessionData;
    public long CurrentAuthKeyId => PermAuthKeyId != 0 ? PermAuthKeyId : AuthKeyId;
    public long AuthKeyId { get; set; }
    public long PermAuthKeyId { get; set; }
    public long Salt { get; set; } = 0;
    public long SessionId { get; set; } = 0;
    public long MessageId { get; set; } = 0;
    public int SequenceNo { get; set; } = 0;
    public int? QuickAck { get; set; }
    public string IP { get; set; } = "";
}
