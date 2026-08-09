// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Calls;

public enum GroupCallRecordingRuntimeStatus
{
    Stopped,
    NotConfigured,
    Starting,
    Ready,
    Degraded,
}

public sealed record GroupCallRecordingRuntimeSnapshot(
    GroupCallRecordingRuntimeStatus Status, int RecoveredRecordings,
    string? Failure);

public sealed class GroupCallRecordingRuntime
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    private readonly IGroupCallRecorder _recorder;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GroupCallRecordingOptions _options;
    private readonly ILogger _log;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private CancellationTokenSource? _stopping;
    private Task? _poller;
    private volatile GroupCallRecordingRuntimeSnapshot _snapshot =
        new(GroupCallRecordingRuntimeStatus.Stopped, 0, null);

    public GroupCallRecordingRuntime(IGroupCallRecorder recorder,
        IUnitOfWork unitOfWork, IGroupCallsRepository groupCallsRepository, GroupCallRecordingOptions options, ILogger log)
    {
        _groupCallsRepository = groupCallsRepository;

        options.Validate();
        _recorder = recorder;
        _unitOfWork = unitOfWork;
        _options = options;
        _log = log;
    }

    public GroupCallRecordingRuntimeSnapshot Snapshot => _snapshot;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (_stopping != null)
            {
                return;
            }
            _snapshot = new(GroupCallRecordingRuntimeStatus.Starting, 0, null);
            _stopping = new CancellationTokenSource();
            if (_recorder is UnavailableGroupCallRecorder)
            {
                await _recorder.StartAsync(cancellationToken);
                _snapshot = new(GroupCallRecordingRuntimeStatus.NotConfigured, 0,
                    "group-call recorder is not configured");
                return;
            }
            try
            {
                await _recorder.StartAsync(cancellationToken);
                int recovered = await RecoverAsync(cancellationToken);
                GroupCallRecordingHealth health = await _recorder
                    .HealthAsync(cancellationToken);
                _snapshot = health.Healthy
                    ? new(GroupCallRecordingRuntimeStatus.Ready, recovered, null)
                    : new(GroupCallRecordingRuntimeStatus.Degraded, recovered,
                        "recording worker reported unhealthy");
            }
            catch (Exception e) when (IsOperationalFailure(e))
            {
                _snapshot = new(GroupCallRecordingRuntimeStatus.Degraded, 0,
                    e.Message);
                _log.Warning(e,
                    "group-call recording startup is degraded; MTProto remains available");
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
                _snapshot = new(GroupCallRecordingRuntimeStatus.Stopped, 0, null);
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
            await _recorder.StopAsync(cancellationToken);
            _poller = null;
            _stopping.Dispose();
            _stopping = null;
            _snapshot = new(GroupCallRecordingRuntimeStatus.Stopped, 0, null);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task<int> RecoverAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TLDto.TLGroupCallState> calls = await _groupCallsRepository.GetActiveCallsAsync(cancellationToken);
        int recovered = 0;
        foreach (TLDto.TLGroupCallState owned in calls)
        {
            using (owned)
            {
                var view = owned.AsGroupCallState();
                if (!view.Flags[9] || !view.Flags[10] || !view.Flags[12])
                {
                    continue;
                }
                long callId = view.Id;
                int generation = view.RecordingGeneration;
                int recordStartDate = view.RecordStartDate;
                long recordingUserId = view.RecordingUserId;
                string title = view.Flags[11]
                    ? Encoding.UTF8.GetString(view.RecordingTitle)
                    : string.Empty;
                bool video = view.RecordVideoActive;
                bool portrait = view.RecordVideoPortrait;
                if (generation <= 0)
                {
                    continue;
                }
                await _recorder.StartRecordingAsync(new GroupCallRecordingRequest(
                    callId, generation, recordStartDate, recordingUserId, title,
                    video, portrait), cancellationToken);
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
                    GroupCallRecordingHealth health = await _recorder
                        .HealthAsync(cancellationToken);
                    _snapshot = health.Healthy
                        ? _snapshot with
                        {
                            Status = GroupCallRecordingRuntimeStatus.Ready,
                            Failure = null
                        }
                        : _snapshot with
                        {
                            Status = GroupCallRecordingRuntimeStatus.Degraded,
                            Failure = "recording worker reported unhealthy"
                        };
                }
                catch (Exception e) when (IsOperationalFailure(e))
                {
                    _snapshot = _snapshot with
                    {
                        Status = GroupCallRecordingRuntimeStatus.Degraded,
                        Failure = e.Message
                    };
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static bool IsOperationalFailure(Exception exception) =>
        exception is GroupCallRecordingException or HttpRequestException or IOException;
}
