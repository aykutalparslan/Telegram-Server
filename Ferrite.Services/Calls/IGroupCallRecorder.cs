// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

public interface IGroupCallRecorder
{
    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    ValueTask StartRecordingAsync(GroupCallRecordingRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<GroupCallRecordingFile> FinalizeRecordingAsync(long callId,
        int generation, CancellationToken cancellationToken = default);

    ValueTask AcknowledgeRecordingAsync(long callId, int generation,
        CancellationToken cancellationToken = default);

    ValueTask<bool> CancelRecordingAsync(long callId, int generation,
        CancellationToken cancellationToken = default);

    ValueTask<GroupCallRecordingHealth> HealthAsync(
        CancellationToken cancellationToken = default);
}
