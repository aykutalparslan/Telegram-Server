// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Calls;

public enum GroupCallRecordingTransitionStatus
{
    Started,
    Stopped,
    NoChange,
    InvalidState,
    MediaUnavailable,
}

public sealed record GroupCallRecordingTransitionResult(
    GroupCallRecordingTransitionStatus Status, TLDto.TLGroupCallState? Call);

public interface IGroupCallRecordingCoordinator
{
    ValueTask<GroupCallRecordingTransitionResult> ToggleAsync(long callId,
        bool start, long initiatingUserId, string title, bool video, bool portrait,
        int now, CancellationToken cancellationToken = default);

    ValueTask<bool> TryCancelAsync(long callId, int generation,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Serializes the long-running recorder/import transition per call. The durable
/// start row is the recovery intent; stop keeps that intent set until the worker
/// artifact has landed in the document store and its self-message is queued.
/// </summary>
public sealed class GroupCallRecordingCoordinator : IGroupCallRecordingCoordinator
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    private const int GateCount = 256;
    private readonly SemaphoreSlim[] _gates = Enumerable.Range(0, GateCount)
        .Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGroupCallRecorder _recorder;
    private readonly IGroupCallRecordingDelivery _delivery;
    private readonly ILogger _log;

    public GroupCallRecordingCoordinator(IUnitOfWork unitOfWork, IGroupCallsRepository groupCallsRepository,
        IGroupCallRecorder recorder, IGroupCallRecordingDelivery delivery, ILogger log)
    {
        _groupCallsRepository = groupCallsRepository;

        _unitOfWork = unitOfWork;
        _recorder = recorder;
        _delivery = delivery;
        _log = log;
    }

    public async ValueTask<GroupCallRecordingTransitionResult> ToggleAsync(long callId,
        bool start, long initiatingUserId, string title, bool video, bool portrait,
        int now, CancellationToken cancellationToken = default)
    {
        SemaphoreSlim gate = _gates[(int)(unchecked((ulong)callId) % GateCount)];
        await gate.WaitAsync(cancellationToken);
        try
        {
            return start
                ? await StartAsync(callId, initiatingUserId, title, video, portrait,
                    now, cancellationToken)
                : await StopAsync(callId, now, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Drops an abandoned session, used when a call is discarded while recording.
    /// A toggle already holding this call's gate owns its own finalize and
    /// acknowledgement, so cancelling underneath it would destroy a recording that
    /// is about to be delivered; that case is skipped rather than waited on.
    /// </summary>
    public async ValueTask<bool> TryCancelAsync(long callId, int generation,
        CancellationToken cancellationToken = default)
    {
        if (generation <= 0)
        {
            return false;
        }
        SemaphoreSlim gate = _gates[(int)(unchecked((ulong)callId) % GateCount)];
        if (!await gate.WaitAsync(0, cancellationToken))
        {
            return false;
        }
        try
        {
            return await _recorder.CancelRecordingAsync(callId, generation,
                cancellationToken);
        }
        catch (Exception e) when (IsOperationalFailure(e))
        {
            _log.Warning(e, $"group-call recording cancel failed call:{callId} " +
                            $"generation:{generation}");
            return false;
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask<GroupCallRecordingTransitionResult> StartAsync(long callId,
        long initiatingUserId, string title, bool video, bool portrait, int now,
        CancellationToken cancellationToken)
    {
        GroupCallRecordingMutationResult mutation = await _groupCallsRepository.TryStartRecordingAsync(callId, now,
                initiatingUserId, title, video, portrait, cancellationToken);
        if (mutation.Status is GroupCallRecordingMutationStatus.NotFound or
            GroupCallRecordingMutationStatus.InvalidState)
        {
            mutation.Call?.Dispose();
            return new(GroupCallRecordingTransitionStatus.InvalidState, null);
        }
        if (mutation.Call == null)
        {
            return new(GroupCallRecordingTransitionStatus.InvalidState, null);
        }

        TLDto.TLGroupCallState call = mutation.Call.Value;
        GroupCallRecordingRequest request = ReadRequest(call);
        try
        {
            await _recorder.StartRecordingAsync(request, cancellationToken);
        }
        catch (Exception e) when (IsOperationalFailure(e))
        {
            call.Dispose();
            if (mutation.Status == GroupCallRecordingMutationStatus.Started)
            {
                GroupCallRecordingMutationResult rollback = await _groupCallsRepository.TryStopRecordingAsync(callId,
                        request.Generation, cancellationToken);
                rollback.Call?.Dispose();
            }
            _log.Warning(e, $"group-call recording start failed call:{callId} " +
                            $"generation:{request.Generation}");
            return new(GroupCallRecordingTransitionStatus.MediaUnavailable, null);
        }

        return new(mutation.Status == GroupCallRecordingMutationStatus.Started
            ? GroupCallRecordingTransitionStatus.Started
            : GroupCallRecordingTransitionStatus.NoChange, call);
    }

    private async ValueTask<GroupCallRecordingTransitionResult> StopAsync(long callId,
        int now, CancellationToken cancellationToken)
    {
        using TLDto.TLGroupCallState? current = await _groupCallsRepository.GetCallAsync(callId, cancellationToken);
        if (current == null)
        {
            return new(GroupCallRecordingTransitionStatus.InvalidState, null);
        }

        var view = current.Value.AsGroupCallState();
        if (view.State != (int)GroupCallPersistenceState.Active)
        {
            return new(GroupCallRecordingTransitionStatus.InvalidState, null);
        }
        if (!view.Flags[9] || !view.Flags[10] || !view.Flags[12])
        {
            return new(GroupCallRecordingTransitionStatus.NoChange,
                view.Clone().Build());
        }
        int generation = view.RecordingGeneration;
        long userId = view.RecordingUserId;
        string title = view.Flags[11]
            ? Encoding.UTF8.GetString(view.RecordingTitle)
            : string.Empty;

        TLDto.TLGroupCallState stoppedCall;
        StoredMessageWrite storedWrite;
        try
        {
            await using GroupCallRecordingFile file = await _recorder
                .FinalizeRecordingAsync(callId, generation, cancellationToken);
            GroupCallRecordingDocument document = await _delivery.ImportAsync(file,
                cancellationToken);
            storedWrite = await _delivery.StoreAsync(userId, document,
                title, now);
            GroupCallRecordingMutationResult stopped = await _groupCallsRepository.TryStopRecordingAsync(callId, generation,
                    cancellationToken);
            if (stopped.Status != GroupCallRecordingMutationStatus.Stopped ||
                stopped.Call == null)
            {
                stopped.Call?.Dispose();
                return new(GroupCallRecordingTransitionStatus.InvalidState, null);
            }
            await _unitOfWork.SaveAsync();
            stoppedCall = stopped.Call.Value;
        }
        catch (Exception e) when (IsOperationalFailure(e))
        {
            _log.Warning(e, $"group-call recording finalize failed call:{callId} " +
                            $"generation:{generation}");
            return new(GroupCallRecordingTransitionStatus.MediaUnavailable, null);
        }

        try
        {
            await _delivery.PublishAsync(userId, storedWrite);
        }
        catch (Exception e) when (IsOperationalFailure(e))
        {
            _log.Warning(e, $"group-call recording live message delivery failed " +
                            $"call:{callId} generation:{generation}");
        }
        await AcknowledgeAsync(callId, generation, cancellationToken);
        return new(GroupCallRecordingTransitionStatus.Stopped, stoppedCall);
    }

    private async Task AcknowledgeAsync(long callId, int generation,
        CancellationToken cancellationToken)
    {
        try
        {
            await _recorder.AcknowledgeRecordingAsync(callId, generation,
                cancellationToken);
        }
        catch (Exception e) when (IsOperationalFailure(e))
        {
            // Delivery is already durable. The worker artifact is bounded and
            // restart-safe, so a missed acknowledgement is operational cleanup,
            // not a reason to revoke a message the user has received.
            _log.Warning(e, $"group-call recording acknowledgement failed " +
                            $"call:{callId} generation:{generation}");
        }
    }

    private static GroupCallRecordingRequest ReadRequest(
        TLDto.TLGroupCallState call)
    {
        var view = call.AsGroupCallState();
        return new GroupCallRecordingRequest(view.Id, view.RecordingGeneration,
            view.RecordStartDate, view.RecordingUserId,
            view.Flags[11] ? Encoding.UTF8.GetString(view.RecordingTitle) : string.Empty,
            view.RecordVideoActive, view.RecordVideoPortrait);
    }

    private static bool IsOperationalFailure(Exception exception) =>
        exception is GroupCallRecordingException or HttpRequestException or IOException;
}
