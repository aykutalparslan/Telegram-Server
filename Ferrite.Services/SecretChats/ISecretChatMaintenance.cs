// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.SecretChats;

public sealed record SecretChatMaintenanceRunResult(
    int AuthKeys,
    int RecoveredPending,
    int ExpiredEvents,
    long ExpiredBytes,
    int DeletedReceipts,
    int DeletedControls,
    int QueuedEvents,
    long QueuedBytes);

public interface ISecretChatMaintenance
{
    ValueTask StartAsync(CancellationToken cancellationToken = default);
    ValueTask StopAsync(CancellationToken cancellationToken = default);
    ValueTask<SecretChatMaintenanceRunResult> RunOnceAsync(
        CancellationToken cancellationToken = default);
}
