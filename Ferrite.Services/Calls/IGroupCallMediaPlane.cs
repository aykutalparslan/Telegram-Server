// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

public sealed record GroupCallMediaJoinRequest(
    long CallId,
    string ParticipantId,
    GroupCallJoinPayload Payload);

public sealed record GroupCallParticipantVideoSources(
    string Endpoint,
    IReadOnlyList<GroupCallVideoSourceGroup> SourceGroups,
    int AudioSource,
    bool Paused);

public sealed record GroupCallViewerSources(
    int AudioSource,
    GroupCallParticipantVideoSources? Video,
    GroupCallParticipantVideoSources? Presentation);

public sealed record GroupCallMediaJoinResult(
    GroupCallMediaTransport Transport,
    int CanonicalSource,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, GroupCallViewerSources>>
        ViewerSources);

public sealed record GroupCallMediaHealth(bool Healthy, int Rooms,
    string? InstanceId = null, string? WorkerVersion = null);

public enum GroupCallMediaDisconnectReason
{
    TransportClosed,

    WorkerDied,
}

public sealed record GroupCallMediaDisconnectEvent(
    long CallId,
    string ParticipantId,
    GroupCallMediaDisconnectReason Reason);

public sealed record GroupCallMediaSourcesChangedEvent(
    long CallId,
    string ParticipantId,
    string Reason);

public enum GroupCallMediaFailureKind
{
    Unavailable,

    Rejected,

    Conflict,

    Timeout,
}

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

public interface IGroupCallMediaPlane
{
    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    ValueTask CreateRoomAsync(long callId, CancellationToken cancellationToken = default);

    ValueTask<bool> EndRoomAsync(long callId, CancellationToken cancellationToken = default);

    ValueTask<GroupCallMediaJoinResult> JoinAsync(GroupCallMediaJoinRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<bool> LeaveAsync(long callId, string participantId,
        CancellationToken cancellationToken = default);

    ValueTask<GroupCallMediaJoinResult> JoinPresentationAsync(
        GroupCallMediaJoinRequest request, CancellationToken cancellationToken = default);

    ValueTask<bool> LeavePresentationAsync(long callId, string participantId,
        CancellationToken cancellationToken = default);

    ValueTask SetVideoPausedAsync(long callId, string participantId, bool paused,
        CancellationToken cancellationToken = default);

    ValueTask SetIngressMuteAsync(long callId, string participantId, bool muted,
        CancellationToken cancellationToken = default);

    ValueTask<bool> IsAliveAsync(long callId, string participantId,
        CancellationToken cancellationToken = default);

    ValueTask<GroupCallMediaHealth> HealthAsync(CancellationToken cancellationToken = default);

    IDisposable Subscribe(Action<GroupCallMediaDisconnectEvent> handler);

    IDisposable SubscribeSourcesChanged(
        Action<GroupCallMediaSourcesChangedEvent> handler);

    ValueTask<IReadOnlyDictionary<string,
        IReadOnlyDictionary<string, GroupCallViewerSources>>> ReadViewerSourcesAsync(
        long callId, CancellationToken cancellationToken = default);
}
