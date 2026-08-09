// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

public interface IGroupCallBroadcastPlane
{
    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    ValueTask CreateStreamAsync(long callId, bool rtmpStream,
        CancellationToken cancellationToken = default);

    ValueTask<bool> EndStreamAsync(long callId,
        CancellationToken cancellationToken = default);

    ValueTask<GroupCallBroadcastCredentials> GetCredentialsAsync(long callId,
        bool revoke, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<GroupCallBroadcastChannel>> GetChannelsAsync(long callId,
        CancellationToken cancellationToken = default);

    ValueTask<ReadOnlyMemory<byte>> ReadSegmentAsync(
        GroupCallBroadcastSegmentRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<GroupCallBroadcastHealth> HealthAsync(
        CancellationToken cancellationToken = default);
}
