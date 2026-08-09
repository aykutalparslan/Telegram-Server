// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using DotNext.Buffers;

namespace Ferrite.Transport;

public interface ITransportConnection
{
    public IDuplexPipe Transport { get; }

    public void Write(ReadOnlySequence<byte> buffer)
    {
        Transport.Output.Write(buffer);
    }
    public ValueTask<FlushResult> FlushAsync()
    {
        return Transport.Output.FlushAsync();
    }
    public EndPoint? RemoteEndPoint { get; }
    public void Start();
    public void Abort(Exception abortReason);
    public ValueTask DisposeAsync();
}

