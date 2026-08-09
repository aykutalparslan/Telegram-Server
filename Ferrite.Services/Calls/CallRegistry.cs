// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Crypto;

namespace Ferrite.Services.Calls;

public sealed class CallRegistry : ICallRegistry, IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<long, CallEntry> _calls = new();
    private readonly Dictionary<(long CallerUserId, int RandomId), long> _dedup = new();
    private readonly Dictionary<long, int> _activePerUser = new();
    private readonly Dictionary<long, Queue<long>> _requestTimestamps = new();
    private readonly CallRegistryOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly IRandomGenerator _random;
    private Action<long, CallDeadlineKind>? _deadlineExpired;
    private int _activeCallCount;
    private int _tombstoneCount;
    private long _rejectedRequestCount;
    private bool _disposed;

    public CallRegistry(CallRegistryOptions options, TimeProvider timeProvider,
        IRandomGenerator random)
    {
        _options = options;
        _timeProvider = timeProvider;
        _random = random;
    }

    public int ActiveCallCount
    {
        get
        {
            lock (_gate)
            {
                return _activeCallCount;
            }
        }
    }

    public int TombstoneCount
    {
        get
        {
            lock (_gate)
            {
                return _tombstoneCount;
            }
        }
    }

    public long RejectedRequestCount
    {
        get
        {
            lock (_gate)
            {
                return _rejectedRequestCount;
            }
        }
    }

    public void SetDeadlineExpiredHandler(Action<long, CallDeadlineKind>? handler)
    {
        lock (_gate)
        {
            _deadlineExpired = handler;
        }
    }

    public CallRegistryResult TryCreate(CallCreateRequest request)
    {
        CallEntry entry;
        lock (_gate)
        {
            if (_dedup.TryGetValue((request.CallerUserId, request.RandomId),
                    out long existingId) &&
                _calls.TryGetValue(existingId, out CallEntry? existing))
            {
                if (existing.CalleeUserId == request.CalleeUserId &&
                    existing.Video == request.Video &&
                    existing.GAHash.AsSpan().SequenceEqual(request.GAHash))
                {
                    return new CallRegistryResult(CallRegistryStatus.Duplicate,
                        existing.Snapshot());
                }

                _rejectedRequestCount++;
                return new CallRegistryResult(CallRegistryStatus.DedupConflict, null);
            }

            long nowTicks = _timeProvider.GetUtcNow().UtcTicks;
            Queue<long> window = GetRequestWindow(request.CallerUserId, nowTicks);
            if (window.Count >= _options.MaxRequestsPerWindow)
            {
                _rejectedRequestCount++;
                return new CallRegistryResult(CallRegistryStatus.RateLimited, null);
            }

            if (_activePerUser.GetValueOrDefault(request.CallerUserId) >=
                _options.MaxActiveCallsPerUser)
            {
                _rejectedRequestCount++;
                return new CallRegistryResult(CallRegistryStatus.QuotaExceeded, null);
            }

            if (_activeCallCount >= _options.MaxTotalCalls)
            {
                _rejectedRequestCount++;
                return new CallRegistryResult(CallRegistryStatus.RegistryFull, null);
            }

            window.Enqueue(nowTicks);
            long callId = NextCallId();
            long accessHash = NextNonZeroLong();
            entry = new CallEntry(callId, accessHash, request);
            _calls.Add(callId, entry);
            _dedup.Add((request.CallerUserId, request.RandomId), callId);
            _activePerUser[request.CallerUserId] =
                _activePerUser.GetValueOrDefault(request.CallerUserId) + 1;
            _activeCallCount++;
            entry.ReceiveDeadline = ScheduleDeadline(callId, CallDeadlineKind.Receive,
                _options.ReceiveDeadline);
            return new CallRegistryResult(CallRegistryStatus.Ok, entry.Snapshot());
        }
    }

    public CallRegistryResult TryMarkReceived(long callId, long accessHash,
        long calleeUserId, int date)
    {
        lock (_gate)
        {
            CallRegistryStatus status = Resolve(callId, accessHash, out CallEntry? entry);
            if (status != CallRegistryStatus.Ok)
            {
                return new CallRegistryResult(status, entry?.Snapshot());
            }

            if (entry!.CalleeUserId != calleeUserId)
            {
                return new CallRegistryResult(CallRegistryStatus.WrongUser, null);
            }

            switch (entry.State)
            {
                case CallSessionState.Discarded:
                    return new CallRegistryResult(CallRegistryStatus.AlreadyDiscarded,
                        entry.Snapshot());
                case CallSessionState.Received:
                case CallSessionState.Accepted:
                case CallSessionState.Confirmed:
                    // Idempotent acknowledgement: the original receive date and
                    // the running ring deadline stay untouched.
                    return new CallRegistryResult(CallRegistryStatus.Ok,
                        entry.Snapshot());
            }

            entry.State = CallSessionState.Received;
            entry.ReceiveDate = date;
            CancelDeadline(ref entry.ReceiveDeadline);
            entry.RingDeadline = ScheduleDeadline(callId, CallDeadlineKind.Ring,
                _options.RingDeadline);
            return new CallRegistryResult(CallRegistryStatus.Ok, entry.Snapshot());
        }
    }

    public CallRegistryResult TryAccept(long callId, long accessHash,
        long calleeUserId, long calleeAuthKeyId, byte[] gB,
        CallProtocol calleeProtocol, CallProtocol negotiatedProtocol, int date)
    {
        lock (_gate)
        {
            CallRegistryStatus status = Resolve(callId, accessHash, out CallEntry? entry);
            if (status != CallRegistryStatus.Ok)
            {
                return new CallRegistryResult(status, entry?.Snapshot());
            }

            if (entry!.CalleeUserId != calleeUserId)
            {
                return new CallRegistryResult(CallRegistryStatus.WrongUser, null);
            }

            switch (entry.State)
            {
                case CallSessionState.Discarded:
                    return new CallRegistryResult(CallRegistryStatus.AlreadyDiscarded,
                        entry.Snapshot());
                case CallSessionState.Accepted:
                case CallSessionState.Confirmed:
                    return new CallRegistryResult(CallRegistryStatus.AlreadyAccepted,
                        entry.Snapshot());
            }

            entry.State = CallSessionState.Accepted;
            entry.CalleeAuthKeyId = calleeAuthKeyId;
            entry.GB = gB.ToArray();
            entry.CalleeProtocol = calleeProtocol;
            entry.NegotiatedProtocol = negotiatedProtocol;
            entry.AcceptDate = date;
            CancelDeadline(ref entry.ReceiveDeadline);
            CancelDeadline(ref entry.RingDeadline);
            return new CallRegistryResult(CallRegistryStatus.Ok, entry.Snapshot());
        }
    }

    public CallRegistryResult TryConfirm(long callId, long accessHash,
        long callerAuthKeyId, byte[] gA, long keyFingerprint, bool p2pAllowed,
        IReadOnlyList<byte[]> connections, byte[]? reflectorAllocationKey,
        int startDate)
    {
        lock (_gate)
        {
            CallRegistryStatus status = Resolve(callId, accessHash, out CallEntry? entry);
            if (status != CallRegistryStatus.Ok)
            {
                return new CallRegistryResult(status, entry?.Snapshot());
            }

            if (entry!.CallerAuthKeyId != callerAuthKeyId)
            {
                return new CallRegistryResult(CallRegistryStatus.WrongDevice, null);
            }

            switch (entry.State)
            {
                case CallSessionState.Discarded:
                    return new CallRegistryResult(CallRegistryStatus.AlreadyDiscarded,
                        entry.Snapshot());
                case CallSessionState.Confirmed:
                    // Duplicate successful confirm returns the same immutable
                    // final call; the caller must not create a second
                    // allocation or fresh credentials.
                    return new CallRegistryResult(CallRegistryStatus.Duplicate,
                        entry.Snapshot());
                case CallSessionState.Requested:
                case CallSessionState.Received:
                    return new CallRegistryResult(CallRegistryStatus.InvalidState,
                        entry.Snapshot());
            }

            entry.State = CallSessionState.Confirmed;
            entry.GA = gA.ToArray();
            entry.KeyFingerprint = keyFingerprint;
            entry.P2pAllowed = p2pAllowed;
            entry.Connections = CopyConnections(connections);
            entry.ReflectorAllocationKey = reflectorAllocationKey?.ToArray();
            entry.StartDate = startDate;
            CancelDeadline(ref entry.ReceiveDeadline);
            CancelDeadline(ref entry.RingDeadline);
            return new CallRegistryResult(CallRegistryStatus.Ok, entry.Snapshot());
        }
    }

    public CallRegistryResult TryDiscard(long callId, long accessHash,
        long requesterUserId, long requesterAuthKeyId, int reasonConstructor,
        int duration, long connectionId, int date)
    {
        lock (_gate)
        {
            CallRegistryStatus status = Resolve(callId, accessHash, out CallEntry? entry);
            if (status != CallRegistryStatus.Ok)
            {
                return new CallRegistryResult(status, entry?.Snapshot());
            }

            if (requesterUserId == entry!.CallerUserId)
            {
                if (requesterAuthKeyId != entry.CallerAuthKeyId)
                {
                    return new CallRegistryResult(CallRegistryStatus.WrongDevice, null);
                }
            }
            else if (requesterUserId == entry.CalleeUserId)
            {
                // Before a winner exists any callee device may decline; after
                // accept only the winning device may end the call.
                if (entry.CalleeAuthKeyId is long winner &&
                    winner != requesterAuthKeyId)
                {
                    return new CallRegistryResult(CallRegistryStatus.WrongDevice, null);
                }
            }
            else
            {
                return new CallRegistryResult(CallRegistryStatus.WrongUser, null);
            }

            if (entry.State == CallSessionState.Discarded)
            {
                return new CallRegistryResult(CallRegistryStatus.AlreadyDiscarded,
                    entry.Snapshot());
            }

            Terminate(entry, reasonConstructor, duration, connectionId);
            return new CallRegistryResult(CallRegistryStatus.Ok, entry.Snapshot());
        }
    }

    public CallRegistryResult TryExpire(long callId, CallDeadlineKind kind,
        int reasonConstructor, int date)
    {
        lock (_gate)
        {
            if (!_calls.TryGetValue(callId, out CallEntry? entry))
            {
                return new CallRegistryResult(CallRegistryStatus.NotFound, null);
            }

            if (entry.State == CallSessionState.Discarded)
            {
                return new CallRegistryResult(CallRegistryStatus.AlreadyDiscarded,
                    entry.Snapshot());
            }

            // A deadline may only expire the state it was armed for; a racing
            // accept or received transition already invalidated it.
            bool valid = kind == CallDeadlineKind.Receive
                ? entry.State == CallSessionState.Requested
                : entry.State == CallSessionState.Received;
            if (!valid)
            {
                return new CallRegistryResult(CallRegistryStatus.InvalidState,
                    entry.Snapshot());
            }

            Terminate(entry, reasonConstructor, duration: 0, connectionId: 0);
            return new CallRegistryResult(CallRegistryStatus.Ok, entry.Snapshot());
        }
    }

    public CallRegistryResult TryMarkCallLogWritten(long callId)
    {
        lock (_gate)
        {
            if (!_calls.TryGetValue(callId, out CallEntry? entry))
            {
                return new CallRegistryResult(CallRegistryStatus.NotFound, null);
            }

            if (entry.Discard == null)
            {
                return new CallRegistryResult(CallRegistryStatus.InvalidState,
                    entry.Snapshot());
            }

            if (entry.Discard.LogWritten)
            {
                return new CallRegistryResult(CallRegistryStatus.LogAlreadyWritten,
                    entry.Snapshot());
            }

            entry.Discard = entry.Discard with { LogWritten = true };
            return new CallRegistryResult(CallRegistryStatus.Ok, entry.Snapshot());
        }
    }

    public CallSnapshot? Get(long callId)
    {
        lock (_gate)
        {
            return _calls.TryGetValue(callId, out CallEntry? entry)
                ? entry.Snapshot()
                : null;
        }
    }

    public CallSnapshot? GetByDedupKey(long callerUserId, int randomId)
    {
        lock (_gate)
        {
            return _dedup.TryGetValue((callerUserId, randomId), out long callId) &&
                   _calls.TryGetValue(callId, out CallEntry? entry)
                ? entry.Snapshot()
                : null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (CallEntry entry in _calls.Values)
            {
                CancelDeadline(ref entry.ReceiveDeadline);
                CancelDeadline(ref entry.RingDeadline);
                CancelDeadline(ref entry.TombstoneTimer);
            }

            _calls.Clear();
            _dedup.Clear();
            _activePerUser.Clear();
            _requestTimestamps.Clear();
        }
    }

    private void Terminate(CallEntry entry, int reasonConstructor, int duration,
        long connectionId)
    {
        entry.Discard = new CallDiscardInfo(entry.State, reasonConstructor, duration,
            connectionId, NeedRating: false, NeedDebug: false, LogWritten: false);
        entry.State = CallSessionState.Discarded;
        CancelDeadline(ref entry.ReceiveDeadline);
        CancelDeadline(ref entry.RingDeadline);
        _activeCallCount--;
        _tombstoneCount++;
        int callerActive = _activePerUser.GetValueOrDefault(entry.CallerUserId) - 1;
        if (callerActive <= 0)
        {
            _activePerUser.Remove(entry.CallerUserId);
        }
        else
        {
            _activePerUser[entry.CallerUserId] = callerActive;
        }

        long callId = entry.CallId;
        entry.TombstoneTimer = _timeProvider.CreateTimer(
            _ => RemoveTombstone(callId), null, _options.TombstoneTtl,
            Timeout.InfiniteTimeSpan);
    }

    private void RemoveTombstone(long callId)
    {
        lock (_gate)
        {
            if (!_calls.TryGetValue(callId, out CallEntry? entry) ||
                entry.State != CallSessionState.Discarded)
            {
                return;
            }

            CancelDeadline(ref entry.TombstoneTimer);
            _calls.Remove(callId);
            _dedup.Remove((entry.CallerUserId, entry.RandomId));
            _tombstoneCount--;
        }
    }

    private CallRegistryStatus Resolve(long callId, long accessHash,
        out CallEntry? entry)
    {
        if (!_calls.TryGetValue(callId, out entry))
        {
            return CallRegistryStatus.NotFound;
        }

        if (entry.AccessHash != accessHash)
        {
            entry = null;
            return CallRegistryStatus.AccessHashInvalid;
        }

        return CallRegistryStatus.Ok;
    }

    private Queue<long> GetRequestWindow(long userId, long nowTicks)
    {
        if (!_requestTimestamps.TryGetValue(userId, out Queue<long>? window))
        {
            window = new Queue<long>();
            _requestTimestamps[userId] = window;
        }

        long cutoff = nowTicks - _options.RequestRateWindow.Ticks;
        while (window.Count > 0 && window.Peek() <= cutoff)
        {
            window.Dequeue();
        }

        if (window.Count == 0)
        {
            _requestTimestamps.Remove(userId);
            _requestTimestamps[userId] = window;
        }

        return window;
    }

    private ITimer ScheduleDeadline(long callId, CallDeadlineKind kind,
        TimeSpan dueIn)
    {
        return _timeProvider.CreateTimer(_ =>
        {
            Action<long, CallDeadlineKind>? handler;
            lock (_gate)
            {
                handler = _deadlineExpired;
            }

            handler?.Invoke(callId, kind);
        }, null, dueIn, Timeout.InfiniteTimeSpan);
    }

    private static void CancelDeadline(ref ITimer? timer)
    {
        timer?.Dispose();
        timer = null;
    }

    private long NextCallId()
    {
        while (true)
        {
            long candidate = _random.NextLong() & long.MaxValue;
            if (candidate != 0 && !_calls.ContainsKey(candidate))
            {
                return candidate;
            }
        }
    }

    private long NextNonZeroLong()
    {
        while (true)
        {
            long candidate = _random.NextLong();
            if (candidate != 0)
            {
                return candidate;
            }
        }
    }

    private static IReadOnlyList<byte[]> CopyConnections(
        IReadOnlyList<byte[]> connections)
    {
        var copy = new byte[connections.Count][];
        for (int i = 0; i < connections.Count; i++)
        {
            copy[i] = connections[i].ToArray();
        }

        return copy;
    }

    private sealed class CallEntry
    {
        public CallEntry(long callId, long accessHash, CallCreateRequest request)
        {
            CallId = callId;
            AccessHash = accessHash;
            CallerUserId = request.CallerUserId;
            CallerAuthKeyId = request.CallerAuthKeyId;
            CalleeUserId = request.CalleeUserId;
            RandomId = request.RandomId;
            Video = request.Video;
            GAHash = request.GAHash.ToArray();
            CallerProtocol = request.Protocol;
            Date = request.Date;
            State = CallSessionState.Requested;
        }

        public long CallId { get; }
        public long AccessHash { get; }
        public long CallerUserId { get; }
        public long CallerAuthKeyId { get; }
        public long CalleeUserId { get; }
        public int RandomId { get; }
        public bool Video { get; }
        public byte[] GAHash { get; }
        public CallProtocol CallerProtocol { get; }
        public int Date { get; }

        public CallSessionState State;
        public long? CalleeAuthKeyId;
        public int? ReceiveDate;
        public int? AcceptDate;
        public int? StartDate;
        public byte[]? GB;
        public byte[]? GA;
        public long? KeyFingerprint;
        public CallProtocol? CalleeProtocol;
        public CallProtocol? NegotiatedProtocol;
        public bool P2pAllowed;
        public IReadOnlyList<byte[]>? Connections;
        public byte[]? ReflectorAllocationKey;
        public CallDiscardInfo? Discard;
        public ITimer? ReceiveDeadline;
        public ITimer? RingDeadline;
        public ITimer? TombstoneTimer;

        public CallSnapshot Snapshot() => new(CallId, AccessHash, CallerUserId,
            CalleeUserId, CallerAuthKeyId, CalleeAuthKeyId, RandomId, Video, State,
            Date, ReceiveDate, AcceptDate, StartDate, GAHash, GB, GA, KeyFingerprint,
            CallerProtocol, CalleeProtocol, NegotiatedProtocol, P2pAllowed,
            Connections, ReflectorAllocationKey, Discard);
    }
}
