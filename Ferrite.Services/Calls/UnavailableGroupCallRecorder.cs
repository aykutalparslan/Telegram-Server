// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

public sealed class UnavailableGroupCallRecorder : IGroupCallRecorder
{
    public Task StartAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public ValueTask StartRecordingAsync(GroupCallRecordingRequest request,
        CancellationToken cancellationToken = default) => Throw();

    public ValueTask<GroupCallRecordingFile> FinalizeRecordingAsync(long callId,
        int generation, CancellationToken cancellationToken = default) =>
        Throw<GroupCallRecordingFile>();

    public ValueTask AcknowledgeRecordingAsync(long callId, int generation,
        CancellationToken cancellationToken = default) => Throw();

    public ValueTask<bool> CancelRecordingAsync(long callId, int generation,
        CancellationToken cancellationToken = default) => Throw<bool>();

    public ValueTask<GroupCallRecordingHealth> HealthAsync(
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new GroupCallRecordingHealth(false, 0, 0, 0, null));

    private static ValueTask Throw() => ValueTask.FromException(
        new GroupCallRecordingException(GroupCallRecordingFailureKind.Unavailable,
            "group-call recorder is not configured"));

    private static ValueTask<T> Throw<T>() => ValueTask.FromException<T>(
        new GroupCallRecordingException(GroupCallRecordingFailureKind.Unavailable,
            "group-call recorder is not configured"));
}
