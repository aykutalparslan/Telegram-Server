// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Calls;

public sealed record GroupCallMediaRuntimeOptions
{
    public TimeSpan HealthInterval { get; init; } = TimeSpan.FromSeconds(5);

    public void Validate()
    {
        if (HealthInterval <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                "group-call media health interval must be positive");
        }
    }
}

public enum GroupCallMediaRuntimeStatus
{
    Starting,
    Ready,
    Degraded,
    Stopped,
}

public sealed record GroupCallMediaRuntimeSnapshot(
    GroupCallMediaRuntimeStatus Status,
    int ActiveCalls,
    int Rooms,
    int StaleParticipants,
    string? WorkerInstanceId,
    string? WorkerVersion,
    string? Failure,
    DateTimeOffset CheckedAtUtc)
{
    public bool IsReady => Status == GroupCallMediaRuntimeStatus.Ready;
}

/// <summary>
/// Owns the external SFU lifecycle, readiness polling, and conservative recovery.
/// Ferrite remains available when this runtime is degraded: create/start/join
/// operations fail through the media plane, while persisted discovery and admin
/// discard continue to work.
/// </summary>
public sealed class GroupCallMediaRuntime
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    private readonly IGroupCallMediaPlane _media;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GroupCallMediaSourceMap _sourceMap;
    private readonly GroupCallMediaRuntimeOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _log;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly SemaphoreSlim _reconcile = new(1, 1);
    private CancellationTokenSource? _stopping;
    private Task? _healthLoop;
    private string? _lastWorkerInstanceId;
    private volatile GroupCallMediaRuntimeSnapshot _snapshot;

    public GroupCallMediaRuntime(IGroupCallMediaPlane media, IUnitOfWork unitOfWork, IGroupCallsRepository groupCallsRepository,
        GroupCallMediaSourceMap sourceMap, GroupCallMediaRuntimeOptions options,
        TimeProvider timeProvider, ILogger log)
    {
        _groupCallsRepository = groupCallsRepository;

        options.Validate();
        _media = media;
        _unitOfWork = unitOfWork;
        _sourceMap = sourceMap;
        _options = options;
        _timeProvider = timeProvider;
        _log = log;
        _snapshot = NewSnapshot(GroupCallMediaRuntimeStatus.Stopped);
    }

    public GroupCallMediaRuntimeSnapshot Snapshot => _snapshot;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (_stopping != null)
            {
                return;
            }

            _snapshot = NewSnapshot(GroupCallMediaRuntimeStatus.Starting);
            await _media.StartAsync(cancellationToken);
            await ReconcileCoreAsync(forceRoomRecovery: true,
                cancellationToken: cancellationToken);

            _stopping = new CancellationTokenSource();
            _healthLoop = MonitorHealthAsync(_stopping.Token);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (_stopping == null)
            {
                _snapshot = NewSnapshot(GroupCallMediaRuntimeStatus.Stopped);
                await _media.StopAsync(cancellationToken);
                return;
            }

            _stopping.Cancel();
            if (_healthLoop != null)
            {
                try
                {
                    await _healthLoop.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                }
            }
            _healthLoop = null;
            _stopping.Dispose();
            _stopping = null;
            await _media.StopAsync(cancellationToken);
            _snapshot = NewSnapshot(GroupCallMediaRuntimeStatus.Stopped);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task<GroupCallMediaRuntimeSnapshot> ReconcileAsync(
        CancellationToken cancellationToken = default) =>
        await ReconcileCoreAsync(
            forceRoomRecovery: _snapshot.Status != GroupCallMediaRuntimeStatus.Ready,
            cancellationToken: cancellationToken);

    private async Task<GroupCallMediaRuntimeSnapshot> ReconcileCoreAsync(
        bool forceRoomRecovery,
        CancellationToken cancellationToken = default)
    {
        await _reconcile.WaitAsync(cancellationToken);
        try
        {
            _snapshot = _snapshot with
            {
                Status = GroupCallMediaRuntimeStatus.Starting,
                Failure = null,
                CheckedAtUtc = _timeProvider.GetUtcNow(),
            };

            IReadOnlyList<TLDto.TLGroupCallState> calls = await _groupCallsRepository.GetActiveCallsAsync(cancellationToken);
            var activeCalls = 0;
            var staleParticipants = 0;
            string? failure = null;
            try
            {
                foreach (TLDto.TLGroupCallState call in calls)
                {
                    var view = call.AsGroupCallState();
                    long callId = view.Id;
                    int state = view.State;
                    if (state != (int)GroupCallPersistenceState.Active)
                    {
                        continue;
                    }

                    activeCalls++;
                    try
                    {
                        GroupCallRecoveryResult recovery = await _groupCallsRepository.TryMarkTransportsStaleAsync(callId,
                                cancellationToken);
                        staleParticipants += recovery.StaleParticipants;
                        if (recovery.Status == GroupCallRecoveryStatus.Reconciled)
                        {
                            _sourceMap.Forget(callId);
                        }

                        if (forceRoomRecovery ||
                            recovery.Status == GroupCallRecoveryStatus.Reconciled)
                        {
                            // Resetting the idempotent room invalidates any worker
                            // transports that survived a Ferrite restart. Doing it
                            // after the durable stale-row mutation means a crash at
                            // either boundary can safely retry and no old transport
                            // is presented as live.
                            await _media.EndRoomAsync(callId, cancellationToken);
                            await _media.CreateRoomAsync(callId, cancellationToken);
                        }
                    }
                    catch (Exception e) when (e is GroupCallMediaException or IOException)
                    {
                        failure ??= $"call {callId} recovery failed: {e.Message}";
                        _log.Warning(e,
                            $"📞 GroupCall recovery degraded call:{callId}");
                    }
                }
            }
            finally
            {
                foreach (TLDto.TLGroupCallState call in calls)
                {
                    call.Dispose();
                }
            }

            GroupCallMediaHealth health;
            try
            {
                health = await _media.HealthAsync(cancellationToken);
                if (!health.Healthy)
                {
                    failure ??= "group-call media worker reported unhealthy";
                }
            }
            catch (GroupCallMediaException e)
            {
                health = new GroupCallMediaHealth(false, 0);
                failure ??= e.Message;
            }

            if (health.Healthy)
            {
                _lastWorkerInstanceId = health.InstanceId;
            }
            _snapshot = new GroupCallMediaRuntimeSnapshot(
                failure == null && health.Healthy
                    ? GroupCallMediaRuntimeStatus.Ready
                    : GroupCallMediaRuntimeStatus.Degraded,
                activeCalls, health.Rooms, staleParticipants, health.InstanceId,
                health.WorkerVersion, failure, _timeProvider.GetUtcNow());
            return _snapshot;
        }
        finally
        {
            _reconcile.Release();
        }
    }

    private async Task MonitorHealthAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.HealthInterval, _timeProvider,
                    cancellationToken);
                GroupCallMediaHealth health = await _media.HealthAsync(cancellationToken);
                bool mustRecover = health.Healthy &&
                    (_snapshot.Status != GroupCallMediaRuntimeStatus.Ready ||
                     health.InstanceId != _lastWorkerInstanceId);
                if (mustRecover)
                {
                    await ReconcileCoreAsync(forceRoomRecovery: true,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    _snapshot = _snapshot with
                    {
                        Status = health.Healthy
                            ? GroupCallMediaRuntimeStatus.Ready
                            : GroupCallMediaRuntimeStatus.Degraded,
                        Rooms = health.Rooms,
                        WorkerInstanceId = health.InstanceId,
                        WorkerVersion = health.WorkerVersion,
                        Failure = health.Healthy ? null :
                            "group-call media worker reported unhealthy",
                        CheckedAtUtc = _timeProvider.GetUtcNow(),
                    };
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (GroupCallMediaException e)
            {
                _snapshot = _snapshot with
                {
                    Status = GroupCallMediaRuntimeStatus.Degraded,
                    Rooms = 0,
                    Failure = e.Message,
                    CheckedAtUtc = _timeProvider.GetUtcNow(),
                };
            }
            catch (Exception e)
            {
                _log.Error(e, "Group-call media health loop failed.");
                _snapshot = _snapshot with
                {
                    Status = GroupCallMediaRuntimeStatus.Degraded,
                    Failure = "group-call media health loop failed",
                    CheckedAtUtc = _timeProvider.GetUtcNow(),
                };
            }
        }
    }

    private GroupCallMediaRuntimeSnapshot NewSnapshot(
        GroupCallMediaRuntimeStatus status) => new(status, 0, 0, 0, null, null,
            null, _timeProvider.GetUtcNow());
}
