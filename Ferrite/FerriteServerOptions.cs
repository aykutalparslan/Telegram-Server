// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.GroupCallMedia;
using Ferrite.Services.Calls;

namespace Ferrite;

/// <summary>
/// Structured server composition options. The MTProto public address and the
/// call-media bind/advertised endpoints are configured separately: binding
/// "0.0.0.0" or an ephemeral port never leaks into advertised rows, and the
/// advertised call-media address only falls back to <see cref="PublicAddress"/>
/// when the operator leaves it empty.
/// </summary>
public sealed record FerriteServerOptions
{
    /// <summary>Public IPv4 literal advertised to MTProto clients.</summary>
    public required string PublicAddress { get; init; }

    /// <summary>MTProto TCP port advertised to clients.</summary>
    public required int Port { get; init; }

    /// <summary>Local storage root for the default development stores.</summary>
    public string DataPath { get; init; } = "data";

    /// <summary>
    /// Stable identity used for inter-node update routing. Null preserves the
    /// local development behavior of loading or creating <c>node.guid</c>.
    /// Distributed deployments should configure one distinct value per node.
    /// </summary>
    public Guid? NodeId { get; init; }

    /// <summary>Capability-level storage backends; defaults to the local preset.</summary>
    public StorageOptions Storage { get; init; } = new();

    /// <summary>
    /// Modern tgcalls UDP reflector configuration. Null uses the development
    /// default: bind 0.0.0.0 on an ephemeral port and advertise
    /// <see cref="PublicAddress"/>. Production deployments should pin a fixed
    /// port that their firewall/NAT forwards.
    /// </summary>
    public CallMediaRelayOptions? CallMedia { get; init; }

    /// <summary>
    /// Optional external coturn STUN/TURN endpoint. Null keeps coturn disabled;
    /// calls then rely on the in-process reflector rows only.
    /// </summary>
    public CallTurnOptions? CallTurn { get; init; }

    /// <summary>
    /// External mediasoup SFU control channel. Null is an explicit degraded mode:
    /// ordinary MTProto plus group-call discovery/discard remain available, while
    /// media allocation returns GROUPCALL_MEDIA_UNAVAILABLE.
    /// </summary>
    public GroupCallMediaWorkerOptions? GroupCallMediaWorker { get; init; }

    /// <summary>Group-call camera capacity advertised to clients.</summary>
    public GroupCallVideoOptions? GroupCallVideo { get; init; }

    /// <summary>SFU readiness polling and restart reconciliation cadence.</summary>
    public GroupCallMediaRuntimeOptions? GroupCallMediaRuntime { get; init; }

    /// <summary>
    /// Broadcast stream discovery/read limits and readiness cadence. The worker
    /// owns RTMP ports and ephemeral segment retention; this object governs the
    /// Telegram-facing single-rendition contract.
    /// </summary>
    public GroupCallBroadcastOptions? GroupCallBroadcast { get; init; }

    /// <summary>
    /// Server-side recording import, finalization, title, and readiness bounds.
    /// A real recorder is composed only when the external group-call worker is
    /// configured; otherwise the runtime reports NotConfigured explicitly.
    /// </summary>
    public GroupCallRecordingOptions? GroupCallRecording { get; init; }
}
