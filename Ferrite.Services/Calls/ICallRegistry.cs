// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

/// <summary>
/// In-memory 1:1 call session registry. Every method performs its full
/// validation and state transition atomically; callers never receive a
/// mutable session object. The first legal transition wins every race.
/// </summary>
public interface ICallRegistry
{
    CallRegistryResult TryCreate(CallCreateRequest request);

    CallRegistryResult TryMarkReceived(long callId, long accessHash,
        long calleeUserId, int date);

    /// <summary>
    /// Binds the first accepting callee device. The negotiated protocol must
    /// be produced by <see cref="CallProtocolNegotiator"/> before the call;
    /// negotiation is pure, so a losing concurrent accept discards its result.
    /// </summary>
    CallRegistryResult TryAccept(long callId, long accessHash, long calleeUserId,
        long calleeAuthKeyId, byte[] gB, CallProtocol calleeProtocol,
        CallProtocol negotiatedProtocol, int date);

    CallRegistryResult TryConfirm(long callId, long accessHash,
        long callerAuthKeyId, byte[] gA, long keyFingerprint, bool p2pAllowed,
        IReadOnlyList<byte[]> connections, byte[]? reflectorAllocationKey,
        int startDate);

    CallRegistryResult TryDiscard(long callId, long accessHash,
        long requesterUserId, long requesterAuthKeyId, int reasonConstructor,
        int duration, long connectionId, int date);

    /// <summary>
    /// Terminal transition used by deadline expiry; performs the same
    /// exactly-once semantics as an explicit discard with a missed reason.
    /// </summary>
    CallRegistryResult TryExpire(long callId, CallDeadlineKind kind,
        int reasonConstructor, int date);

    CallRegistryResult TryMarkCallLogWritten(long callId);

    CallSnapshot? Get(long callId);

    CallSnapshot? GetByDedupKey(long callerUserId, int randomId);

    /// <summary>
    /// Installs the callback invoked when a receive or ring deadline fires.
    /// The callback runs outside registry locks and must route through
    /// <see cref="TryExpire"/> for the exactly-once terminal transition.
    /// </summary>
    void SetDeadlineExpiredHandler(Action<long, CallDeadlineKind>? handler);

    int ActiveCallCount { get; }

    int TombstoneCount { get; }

    long RejectedRequestCount { get; }
}
