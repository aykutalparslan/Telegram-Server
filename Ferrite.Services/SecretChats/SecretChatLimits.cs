// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.SecretChats;

public sealed record SecretChatLimits
{
    public int MinDhRandomLength { get; init; } = 0;
    public int MaxDhRandomLength { get; init; } = 256;
    public int MinEncryptedMessageBytes { get; init; } = 40;
    public int MaxEncryptedMessageBytes { get; init; } = 1024 * 1024;
    public int MaxPendingChatsPerAuthKey { get; init; } = 100;
    public int MaxActiveChatsPerAuthKey { get; init; } = 1_000;
    public int MaxOutstandingRequestsPerAuthKey { get; init; } = 32;
    public int MaxQtsEventsPerAuthKey { get; init; } = 10_000;
    public long MaxQtsBytesPerAuthKey { get; init; } = 64L * 1024 * 1024;
    public int QtsRetentionSeconds { get; init; } = 7 * 24 * 60 * 60;
    public int DedupRetentionSeconds { get; init; } = 7 * 24 * 60 * 60;
    public int MaxEncryptedFileAssociations { get; init; } = 100;
    public int MaintenanceIntervalSeconds { get; init; } = 5 * 60;
    public int MaxMaintenanceAuthKeysPerPass { get; init; } = 128;
    public int MaxMaintenanceItemsPerPass { get; init; } = 1_000;
}
