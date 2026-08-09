// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

/// <summary>
/// One participant's join request against the media plane. <see cref="CallId"/>
/// is the persisted group-call id; <see cref="ParticipantId"/> is the durable
/// media correlation id (<c>groupCallParticipantState.media_id</c>). The payload
/// is the already-validated tgcalls audio offer.
/// </summary>
public sealed record GroupCallMediaJoinRequest(
    long CallId,
    string ParticipantId,
    GroupCallJoinPayload Payload);

/// <summary>
/// One viewer's view of one producer's video or presentation stream. The source
/// groups are the SSRCs the worker assigned for THIS consumer, not the producer's
/// canonical ones, because mediasoup rewrites per-consumer SSRCs.
/// </summary>
public sealed record GroupCallParticipantVideoSources(
    string Endpoint,
    IReadOnlyList<GroupCallVideoSourceGroup> SourceGroups,
    int AudioSource,
    bool Paused);

/// <summary>
/// Everything one viewer sees of one producer: the rewritten audio source plus
/// the camera and screen-share streams when those transports exist.
/// </summary>
public sealed record GroupCallViewerSources(
    int AudioSource,
    GroupCallParticipantVideoSources? Video,
    GroupCallParticipantVideoSources? Presentation);

/// <summary>
/// The media worker's answer for one join. <see cref="Transport"/> becomes the
/// joiner's connection answer; <see cref="CanonicalSource"/> echoes the joiner's
/// own signed source; <see cref="ViewerSources"/> maps
/// <c>viewerParticipantId -&gt; (producerParticipantId -&gt; that viewer's sources)</c>
/// because the worker rewrites per-consumer SSRCs.
/// </summary>
public sealed record GroupCallMediaJoinResult(
    GroupCallMediaTransport Transport,
    int CanonicalSource,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, GroupCallViewerSources>>
        ViewerSources);

/// <summary>Media-worker health snapshot.</summary>
public sealed record GroupCallMediaHealth(bool Healthy, int Rooms,
    string? InstanceId = null, string? WorkerVersion = null);

public enum GroupCallMediaDisconnectReason
{
    /// <summary>The participant's transport closed (ICE/DTLS lost or torn down).</summary>
    TransportClosed,

    /// <summary>The worker process died; every correlated participant is gone.</summary>
    WorkerDied,
}

/// <summary>
/// A worker-originated disconnect notification carrying the call/participant
/// correlation ids so the control plane can start a grace timer and eventually
/// mark the participant left.
/// </summary>
public sealed record GroupCallMediaDisconnectEvent(
    long CallId,
    string ParticipantId,
    GroupCallMediaDisconnectReason Reason);

/// <summary>
/// The worker re-derived a call's per-viewer media mapping without any request
/// from Ferrite, so the rewritten SSRCs currently published to clients are
/// stale. Today this is a video codec correction: the join answer offers every
/// codec, the client picks one and never says which, and the worker only learns
/// the answer from the RTP that arrives — re-creating the producer and every
/// consumer of it. The control plane must re-read the mapping and re-publish the
/// affected participant rows, or receivers keep listening on dead SSRCs.
/// </summary>
public sealed record GroupCallMediaSourcesChangedEvent(
    long CallId,
    string ParticipantId,
    string Reason);

public enum GroupCallMediaFailureKind
{
    /// <summary>The worker could not be reached or is not healthy.</summary>
    Unavailable,

    /// <summary>The worker rejected the request as invalid.</summary>
    Rejected,

    /// <summary>A conflicting resource exists (duplicate source or participant).</summary>
    Conflict,

    /// <summary>The request exceeded its time budget.</summary>
    Timeout,
}

/// <summary>
/// Raised by <see cref="IGroupCallMediaPlane"/> implementations for every media
/// failure. Handlers translate <see cref="Kind"/> into typed wire errors and
/// compensation. A duplicate source surfaces as <see cref="GroupCallMediaFailureKind.Conflict"/>.
/// </summary>
public sealed class GroupCallMediaException : Exception
{
    public GroupCallMediaException(GroupCallMediaFailureKind kind, string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public GroupCallMediaFailureKind Kind { get; }
}

/// <summary>
/// The narrow, idempotent boundary between Ferrite's group-call control plane and
/// the external self-hosted media worker. The concrete adapter talks to the
/// worker over an authenticated loopback/private channel; the in-memory fake
/// backs deterministic signaling tests. Media allocation succeeds before the
/// participant mutation commits, so every operation is idempotent and bounded.
/// </summary>
public interface IGroupCallMediaPlane
{
    /// <summary>
    /// Start the plane's bounded event reader. This never makes MTProto startup
    /// depend on worker health; readiness reports an unavailable worker instead.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stop and await the event reader. Idempotent.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Create or recover the room for <paramref name="callId"/>. Idempotent.</summary>
    ValueTask CreateRoomAsync(long callId, CancellationToken cancellationToken = default);

    /// <summary>End the room. Returns false when it did not exist. Idempotent.</summary>
    ValueTask<bool> EndRoomAsync(long callId, CancellationToken cancellationToken = default);

    /// <summary>Create the participant transport and return the connection answer.</summary>
    ValueTask<GroupCallMediaJoinResult> JoinAsync(GroupCallMediaJoinRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Tear down the participant transport. Returns false when absent. Idempotent.</summary>
    ValueTask<bool> LeaveAsync(long callId, string participantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create the participant's screen-share transport. Independent of the camera
    /// transport: the camera join survives this failing or being torn down.
    /// Idempotent per participant.
    /// </summary>
    ValueTask<GroupCallMediaJoinResult> JoinPresentationAsync(
        GroupCallMediaJoinRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tear down only the screen-share transport. Returns false when absent.
    /// Idempotent; never affects the camera transport.
    /// </summary>
    ValueTask<bool> LeavePresentationAsync(long callId, string participantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pause or resume a participant's outgoing video without tearing down its
    /// transport. Idempotent.
    /// </summary>
    ValueTask SetVideoPausedAsync(long callId, string participantId, bool paused,
        CancellationToken cancellationToken = default);

    /// <summary>Pause/resume a participant's ingress (global edge mute). Idempotent.</summary>
    ValueTask SetIngressMuteAsync(long callId, string participantId, bool muted,
        CancellationToken cancellationToken = default);

    /// <summary>Whether the participant transport is connected and alive.</summary>
    ValueTask<bool> IsAliveAsync(long callId, string participantId,
        CancellationToken cancellationToken = default);

    /// <summary>Query worker health.</summary>
    ValueTask<GroupCallMediaHealth> HealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribe to disconnect events. Dispose the returned token to unsubscribe.
    /// </summary>
    IDisposable Subscribe(Action<GroupCallMediaDisconnectEvent> handler);

    /// <summary>
    /// Subscribe to worker-initiated media mapping changes. Dispose the returned
    /// token to unsubscribe.
    /// </summary>
    IDisposable SubscribeSourcesChanged(
        Action<GroupCallMediaSourcesChangedEvent> handler);

    /// <summary>
    /// Re-read the whole per-viewer mapping for one call. Join answers already
    /// carry it; this is for changes no request caused.
    /// </summary>
    ValueTask<IReadOnlyDictionary<string,
        IReadOnlyDictionary<string, GroupCallViewerSources>>> ReadViewerSourcesAsync(
        long callId, CancellationToken cancellationToken = default);
}
