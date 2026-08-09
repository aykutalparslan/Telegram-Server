// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

public sealed class UnavailableGroupCallBroadcastPlane : IGroupCallBroadcastPlane
{
    public Task StartAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public ValueTask CreateStreamAsync(long callId, bool rtmpStream,
        CancellationToken cancellationToken = default) => Throw();

    public ValueTask<bool> EndStreamAsync(long callId,
        CancellationToken cancellationToken = default) => Throw<bool>();

    public ValueTask<GroupCallBroadcastCredentials> GetCredentialsAsync(long callId,
        bool revoke, CancellationToken cancellationToken = default) =>
        Throw<GroupCallBroadcastCredentials>();

    public ValueTask<IReadOnlyList<GroupCallBroadcastChannel>> GetChannelsAsync(
        long callId, CancellationToken cancellationToken = default) =>
        Throw<IReadOnlyList<GroupCallBroadcastChannel>>();

    public ValueTask<ReadOnlyMemory<byte>> ReadSegmentAsync(
        GroupCallBroadcastSegmentRequest request,
        CancellationToken cancellationToken = default) =>
        Throw<ReadOnlyMemory<byte>>();

    public ValueTask<GroupCallBroadcastHealth> HealthAsync(
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new GroupCallBroadcastHealth(false, 0, 0, 0, 0, null));

    private static ValueTask Throw() =>
        ValueTask.FromException(new GroupCallBroadcastException(
            GroupCallBroadcastFailureKind.Unavailable,
            "group-call broadcast plane is not configured"));

    private static ValueTask<T> Throw<T>() =>
        ValueTask.FromException<T>(new GroupCallBroadcastException(
            GroupCallBroadcastFailureKind.Unavailable,
            "group-call broadcast plane is not configured"));
}
