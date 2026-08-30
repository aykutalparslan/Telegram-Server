// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC


namespace Ferrite.Services.Transport;

public interface IMTProtoConnection
{
    bool IsEncrypted { get; }
    void Abort(Exception abortReason);
    ValueTask Ping(long pingId, long requestMessageId, int delayDisconnectInSeconds = 0);
    ValueTask SendAsync(MTProtoMessage message);
    ValueTask SendAsync(IFileOwner? message);
    void Start();
}
