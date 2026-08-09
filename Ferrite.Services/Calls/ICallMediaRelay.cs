// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Net;

namespace Ferrite.Services.Calls;

/// <summary>
/// One relay allocation for a confirmed call. PeerTag is the 16-byte client
/// credential (12-byte secure allocation prefix plus 4 server-chosen filler
/// bytes); current clients replace the last four bytes with their own random
/// participant tag.
/// </summary>
public sealed record CallRelayAllocation(long CallId, byte[] PeerTag)
{
    public ReadOnlySpan<byte> Prefix => PeerTag.AsSpan(0, 12);
}

/// <summary>
/// In-process modern tgcalls UDP reflector boundary. The implementation
/// forwards opaque encrypted datagrams between the participants of one
/// allocation; it is not part of the MTProto request chain.
/// </summary>
public interface ICallMediaRelay
{
    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync();

    bool IsReady { get; }

    /// <summary>Actual bound endpoint; resolves port 0 binds for tests.</summary>
    IPEndPoint? BoundEndpoint { get; }

    /// <summary>Endpoint advertised inside phoneConnection rows.</summary>
    IPEndPoint? AdvertisedEndpoint { get; }

    CallRelayAllocation? CreateAllocation(long callId);

    bool RemoveAllocation(long callId);

    int AllocationCount { get; }

    long ForwardedPackets { get; }

    long ForwardedBytes { get; }

    long DroppedPackets { get; }
}
