// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Calls;

public enum GroupCallBroadcastRuntimeStatus
{
    Stopped,
    Starting,
    Ready,
    Degraded,
}

public sealed record GroupCallBroadcastRuntimeSnapshot(
    GroupCallBroadcastRuntimeStatus Status, int RecoveredStreams,
    string? Failure);

public sealed class GroupCallBroadcastRuntime
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    private readonly IGroupCallBroadcastPlane _broadcast;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GroupCallBroadcastOptions _options;
    private readonly ILogger _log;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private CancellationTokenSource? _stopping;
    private Task? _poller;
    private volatile GroupCallBroadcastRuntimeSnapshot _snapshot =
        new(GroupCallBroadcastRuntimeStatus.Stopped, 0, null);

    public GroupCallBroadcastRuntime(IGroupCallBroadcastPlane broadcast,
        IUnitOfWork unitOfWork, IGroupCallsRepository groupCallsRepository, GroupCallBroadcastOptions options, ILogger log)
    {
        _groupCallsRepository = groupCallsRepository;

        options.Validate();
        _broadcast = broadcast;
        _unitOfWork = unitOfWork;
        _options = options;
        _log = log;
    }

    public GroupCallBroadcastRuntimeSnapshot Snapshot => _snapshot;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (_stopping != null)
            {
                return;
            }
            _snapshot = new(GroupCallBroadcastRuntimeStatus.Starting, 0, null);
            _stopping = new CancellationTokenSource();
            try
            {
                await _broadcast.StartAsync(cancellationToken);
                int recovered = await RecoverAsync(cancellationToken);
                GroupCallBroadcastHealth health = await _broadcast
                    .HealthAsync(cancellationToken);
                _snapshot = health.Healthy
                    ? new(GroupCallBroadcastRuntimeStatus.Ready, recovered, null)
                    : new(GroupCallBroadcastRuntimeStatus.Degraded, recovered,
                        "broadcast worker reported unhealthy");
            }
            catch (Exception e) when (e is GroupCallBroadcastException or
                                      HttpRequestException or IOException)
            {
                _snapshot = new(GroupCallBroadcastRuntimeStatus.Degraded, 0,
                    e.Message);
                _log.Warning(e,
                    "group-call broadcast startup is degraded; MTProto remains available");
            }
            _poller = PollAsync(_stopping.Token);
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
                _snapshot = new(GroupCallBroadcastRuntimeStatus.Stopped, 0, null);
                return;
            }
            _stopping.Cancel();
            if (_poller != null)
            {
                try
                {
                    await _poller.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (
                    !cancellationToken.IsCancellationRequested)
                {
                }
            }
            await _broadcast.StopAsync(cancellationToken);
            _poller = null;
            _stopping.Dispose();
            _stopping = null;
            _snapshot = new(GroupCallBroadcastRuntimeStatus.Stopped, 0, null);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task<int> RecoverAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TLDto.TLGroupCallState> calls =
            await _groupCallsRepository.GetActiveCallsAsync(
                cancellationToken);
        int recovered = 0;
        foreach (var owned in calls)
        {
            using (owned)
            {
                var view = owned.AsGroupCallState();
                await _broadcast.CreateStreamAsync(view.Id, view.RtmpStream,
                    cancellationToken);
                recovered++;
            }
        }
        return recovered;
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_options.HealthPollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    GroupCallBroadcastHealth health = await _broadcast
                        .HealthAsync(cancellationToken);
                    _snapshot = health.Healthy
                        ? _snapshot with
                        {
                            Status = GroupCallBroadcastRuntimeStatus.Ready,
                            Failure = null
                        }
                        : _snapshot with
                        {
                            Status = GroupCallBroadcastRuntimeStatus.Degraded,
                            Failure = "broadcast worker reported unhealthy"
                        };
                }
                catch (Exception e) when (e is GroupCallBroadcastException or
                                          HttpRequestException or IOException)
                {
                    _snapshot = _snapshot with
                    {
                        Status = GroupCallBroadcastRuntimeStatus.Degraded,
                        Failure = e.Message
                    };
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
