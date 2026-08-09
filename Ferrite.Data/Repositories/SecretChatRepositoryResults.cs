// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public enum SecretChatPersistenceState
{
    Pending = 0,
    Active = 1,
    Discarded = 2
}

public enum SecretChatCreateStatus
{
    Created,
    Idempotent,
    ChatIdCollision,
    InitiatorRandomIdCollision,
    PairRequestExists,
    PendingLimitExceeded,
    RecipientRequestLimitExceeded,
    RecipientUnavailable,
    AuthKeyRevoked
}

public sealed record SecretChatCreateResult(
    SecretChatCreateStatus Status,
    TLDto.TLSecretChatState? Chat);

public enum SecretChatAcceptStatus
{
    Accepted,
    NotFound,
    NotPending,
    RecipientNotRequested,
    ActiveLimitExceeded,
    AuthKeyRevoked
}

public sealed record SecretChatAcceptResult(
    SecretChatAcceptStatus Status,
    TLDto.TLSecretChatState? Chat,
    IReadOnlyList<long> LosingRecipientAuthKeyIds);

public enum SecretChatDiscardStatus
{
    Discarded,
    AlreadyDiscarded,
    AlreadyAccepted,
    Unauthorized,
    NotFound
}

public sealed record SecretChatDiscardResult(
    SecretChatDiscardStatus Status,
    TLDto.TLSecretChatState? Chat,
    IReadOnlyList<long> NotificationAuthKeyIds);

public enum SecretChatQtsAppendStatus
{
    Appended,
    EventLimitExceeded,
    ByteLimitExceeded,
    AuthKeyRevoked
}

public sealed record SecretChatQtsAppendResult(
    SecretChatQtsAppendStatus Status,
    TLDto.TLSecretChatQtsEntry? Entry);

public enum SecretChatSendAppendStatus
{
    Appended,
    AlreadyExists,
    NotFound,
    NotActive,
    Unauthorized,
    AccessHashInvalid,
    EventLimitExceeded,
    ByteLimitExceeded,
    AuthKeyRevoked
}

public sealed record SecretChatSendAppendResult(
    SecretChatSendAppendStatus Status,
    TLDto.TLSecretChatQtsEntry? Entry,
    TLDto.TLSecretChatSendReceipt? Receipt);

public enum SecretChatQtsConfirmStatus
{
    Confirmed,
    Invalid
}

public sealed record SecretChatQtsConfirmResult(
    SecretChatQtsConfirmStatus Status,
    TLDto.TLSecretChatQtsState State);

public sealed record SecretChatQtsDifferenceResult(
    IReadOnlyList<TLDto.TLSecretChatQtsEntry> Entries,
    TLDto.TLSecretChatQtsState State,
    int HighWaterQts,
    bool HasMore);

public sealed record SecretChatControlDifferenceResult(
    IReadOnlyList<TLDto.TLSecretChatControlUpdate> Updates);

public enum SecretChatReceiptPutStatus
{
    Created,
    AlreadyExists
}

public sealed record SecretChatReceiptPutResult(
    SecretChatReceiptPutStatus Status,
    TLDto.TLSecretChatSendReceipt Receipt);

public enum SecretChatReadAdvanceStatus
{
    Advanced,
    Unchanged,
    NotFound,
    NotActive,
    Unauthorized,
    AccessHashInvalid,
    AuthKeyRevoked
}

public enum SecretChatFileAssociationStatus
{
    Created,
    AlreadyExists,
    LimitExceeded
}

public sealed record SecretChatAuthKeyRevocationResult(
    bool AlreadyRevoked,
    int Date,
    IReadOnlyList<TLDto.TLSecretChatRevokedPeer> AffectedPeers);

public sealed record SecretChatQtsMaintenanceResult(
    long AuthKeyId,
    bool RecoveredPending,
    int ExpiredEvents,
    long ExpiredBytes,
    int AcknowledgedQts,
    int QueuedEvents,
    long QueuedBytes);

public sealed record SecretChatRetentionCleanupResult(
    int ScannedReceipts,
    int DeletedReceipts,
    int ScannedControls,
    int DeletedControls);
