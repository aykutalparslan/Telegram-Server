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

    /// <summary>Actual MTProto TCP endpoint while running; null otherwise.</summary>
    public IPEndPoint? BoundEndpoint { get; }

    /// <summary>Actual call-media UDP endpoint while running; null otherwise.</summary>
    public IPEndPoint? CallMediaBoundEndpoint { get; }

    /// <summary>Read-only call-media counters for diagnostics and tests.</summary>
    public CallMediaRelaySnapshot CallMediaRelaySnapshot { get; }

    /// <summary>
    /// MTProto and each group-media pipeline are reported independently so an SFU
    /// outage never masquerades as a full-server outage. Recording reports
    /// NotConfigured when no external group-call worker is configured.
    /// </summary>
    public FerriteReadinessSnapshot Readiness { get; }
}
