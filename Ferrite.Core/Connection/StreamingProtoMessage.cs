// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Core.Execution;

namespace Ferrite.Core.Connection;

public readonly record struct StreamingProtoMessage
{
    public static StreamingProtoMessage Default { get; } = new();
    public ProtoHeaders Headers { get; init; }
    public MTProtoPipe MessageData { get; init; }
}