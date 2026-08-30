// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Net;
using Ferrite.Services.Calls;

namespace Ferrite.Core;

public readonly record struct CallMediaRelaySnapshot(bool IsReady,
    int AllocationCount, long ForwardedPackets, long ForwardedBytes,
    long DroppedPackets);

public enum FerritePipelineReadinessStatus
{
    NotConfigured,
    Starting,
    Ready,
    Degraded,
    Stopped,
}

public readonly record struct FerritePipelineReadinessSnapshot(
    FerritePipelineReadinessStatus Status, string? Failure);

public readonly record struct FerriteReadinessSnapshot(
    bool MtProtoReady,
    FerritePipelineReadinessSnapshot GroupCallSfu,
    FerritePipelineReadinessSnapshot GroupCallBroadcast,
    FerritePipelineReadinessSnapshot GroupCallRecording);

public interface IFerriteServer
{
    public Task StartAsync(IPEndPoint endPoint, CancellationToken token);
    public Task StopAsync(CancellationToken token);

    public IPEndPoint? BoundEndpoint { get; }

    public IPEndPoint? CallMediaBoundEndpoint { get; }

    public CallMediaRelaySnapshot CallMediaRelaySnapshot { get; }

    public FerriteReadinessSnapshot Readiness { get; }
}
