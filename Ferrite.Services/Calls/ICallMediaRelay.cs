// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Net;

namespace Ferrite.Services.Calls;

public sealed record CallRelayAllocation(long CallId, byte[] PeerTag)
{
    public ReadOnlySpan<byte> Prefix => PeerTag.AsSpan(0, 12);
}

public interface ICallMediaRelay
{
    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync();

    bool IsReady { get; }

    IPEndPoint? BoundEndpoint { get; }

    IPEndPoint? AdvertisedEndpoint { get; }

    CallRelayAllocation? CreateAllocation(long callId);

    bool RemoveAllocation(long callId);

    int AllocationCount { get; }

    long ForwardedPackets { get; }

    long ForwardedBytes { get; }

    long DroppedPackets { get; }
}
