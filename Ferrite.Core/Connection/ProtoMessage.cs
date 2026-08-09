// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;

namespace Ferrite.Core.Connection;

public readonly record struct ProtoMessage : IDisposable
{
    public ProtoHeaders Headers { get; init; }
    public TLBytes MessageData { get; init; }
    public void Dispose()
    {
        MessageData.Dispose();
    }
}