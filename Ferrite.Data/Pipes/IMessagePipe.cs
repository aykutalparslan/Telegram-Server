// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC
using System;
namespace Ferrite.Data.Pipes;

public interface IMessagePipe
{
    public ValueTask<bool> SubscribeAsync(string channel);
    public ValueTask<bool> UnSubscribeAsync();
    public ValueTask<byte[]> ReadMessageAsync(CancellationToken cancellationToken = default);
    public ValueTask<bool> WriteMessageAsync(string channel, byte[] message);
}

