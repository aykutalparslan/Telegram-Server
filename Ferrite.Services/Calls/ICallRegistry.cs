// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

public interface ICallRegistry
{
    CallRegistryResult TryCreate(CallCreateRequest request);

    CallRegistryResult TryMarkReceived(long callId, long accessHash,
        long calleeUserId, int date);

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

    CallRegistryResult TryExpire(long callId, CallDeadlineKind kind,
        int reasonConstructor, int date);

    CallRegistryResult TryMarkCallLogWritten(long callId);

    CallSnapshot? Get(long callId);

    CallSnapshot? GetByDedupKey(long callerUserId, int randomId);

    void SetDeadlineExpiredHandler(Action<long, CallDeadlineKind>? handler);

    int ActiveCallCount { get; }

    int TombstoneCount { get; }

    long RejectedRequestCount { get; }
}
