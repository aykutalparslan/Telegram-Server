// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

public sealed class UnavailableGroupCallMediaPlane : IGroupCallMediaPlane
{
    private static GroupCallMediaException Unavailable() => new(
        GroupCallMediaFailureKind.Unavailable,
        "group-call media worker is not configured");

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public ValueTask CreateRoomAsync(long callId,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException(Unavailable());

    public ValueTask<bool> EndRoomAsync(long callId,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);

    public ValueTask<GroupCallMediaJoinResult> JoinAsync(
        GroupCallMediaJoinRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<GroupCallMediaJoinResult>(Unavailable());

    public ValueTask<bool> LeaveAsync(long callId, string participantId,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);

    public ValueTask<GroupCallMediaJoinResult> JoinPresentationAsync(
        GroupCallMediaJoinRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<GroupCallMediaJoinResult>(Unavailable());

    public ValueTask<bool> LeavePresentationAsync(long callId,
        string participantId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);

    public ValueTask SetVideoPausedAsync(long callId, string participantId,
        bool paused, CancellationToken cancellationToken = default) =>
        ValueTask.FromException(Unavailable());

    public ValueTask SetIngressMuteAsync(long callId, string participantId,
        bool muted, CancellationToken cancellationToken = default) =>
        ValueTask.FromException(Unavailable());

    public ValueTask<bool> IsAliveAsync(long callId, string participantId,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<bool>(Unavailable());

    public ValueTask<GroupCallMediaHealth> HealthAsync(
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new GroupCallMediaHealth(false, 0));

    public IDisposable Subscribe(Action<GroupCallMediaDisconnectEvent> handler) =>
        EmptySubscription.Instance;

    public IDisposable SubscribeSourcesChanged(
        Action<GroupCallMediaSourcesChangedEvent> handler) =>
        EmptySubscription.Instance;

    public ValueTask<IReadOnlyDictionary<string,
        IReadOnlyDictionary<string, GroupCallViewerSources>>> ReadViewerSourcesAsync(
        long callId, CancellationToken cancellationToken = default) =>
        throw Unavailable();

    private sealed class EmptySubscription : IDisposable
    {
        public static EmptySubscription Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
