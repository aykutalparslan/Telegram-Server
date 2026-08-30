// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

public enum CallSessionState
{
    Requested,
    Received,
    Accepted,
    Confirmed,
    Discarded,
}

public enum CallDeadlineKind
{
    Receive,
    Ring,
}

public enum CallRegistryStatus
{
    Ok,
    Duplicate,
    DedupConflict,
    QuotaExceeded,
    RateLimited,
    RegistryFull,
    NotFound,
    AccessHashInvalid,
    WrongUser,
    WrongDevice,
    InvalidState,
    AlreadyAccepted,
    AlreadyDiscarded,
    LogAlreadyWritten,
}

public readonly record struct CallRegistryResult(CallRegistryStatus Status,
    CallSnapshot? Call)
{
    public bool IsOk => Status == CallRegistryStatus.Ok;
}

public sealed record CallCreateRequest(long CallerUserId, long CallerAuthKeyId,
    long CalleeUserId, int RandomId, byte[] GAHash, CallProtocol Protocol,
    bool Video, int Date);

public sealed record CallDiscardInfo(CallSessionState PriorState, int ReasonConstructor,
    int Duration, long ConnectionId, bool NeedRating, bool NeedDebug, bool LogWritten);

public sealed record CallSnapshot(
    long CallId,
    long AccessHash,
    long CallerUserId,
    long CalleeUserId,
    long CallerAuthKeyId,
    long? CalleeAuthKeyId,
    int RandomId,
    bool Video,
    CallSessionState State,
    int Date,
    int? ReceiveDate,
    int? AcceptDate,
    int? StartDate,
    byte[] GAHash,
    byte[]? GB,
    byte[]? GA,
    long? KeyFingerprint,
    CallProtocol CallerProtocol,
    CallProtocol? CalleeProtocol,
    CallProtocol? NegotiatedProtocol,
    bool P2pAllowed,
    IReadOnlyList<byte[]>? Connections,
    byte[]? ReflectorAllocationKey,
    CallDiscardInfo? Discard);
