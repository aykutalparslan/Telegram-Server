// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Calls;

public sealed class GroupCallActivityOptions
{
    // How often one participant's active_date may be refreshed. Speaking events
    // arrive continuously from the media plane; clients only need a coarse
    // "recently active" signal.
    public TimeSpan ParticipantInterval { get; init; } = TimeSpan.FromSeconds(10);

    // Ceiling on refreshes for a single call inside one window, so a large or
    // noisy call cannot turn telemetry into unbounded fan-out.
    public TimeSpan CallWindow { get; init; } = TimeSpan.FromSeconds(1);

    public int MaxRefreshesPerCallWindow { get; init; } = 10;

    // Bounds the per-participant timestamp map. Reaching it drops the oldest
    // entries, which at worst lets a participant refresh one interval early.
    public int MaxTrackedParticipants { get; init; } = 10_000;
}

public enum GroupCallActivityDecision
{
    Refresh,
    ParticipantThrottled,
    CallThrottled,
}

/// <summary>
/// Turns media-plane speaking activity into bounded, rate-limited active_date
/// refreshes. Telemetry is explicitly non-versioned: it never increments the
/// stored call or participant version, and the rows it emits carry no
/// <c>versioned</c> flag, so a client that misses one has nothing to recover.
/// </summary>
public sealed class GroupCallActivityTracker
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    private readonly GroupCallActivityOptions _options;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateFanout _fanout;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _log;
    private readonly object _gate = new();
    private readonly Dictionary<(long CallId, long UserId), long> _lastRefreshTicks = new();
    private readonly Dictionary<long, Queue<long>> _callWindows = new();
    private long _refreshed;
    private long _throttledParticipant;
    private long _throttledCall;

    public GroupCallActivityTracker(GroupCallActivityOptions options,
        IUnitOfWork unitOfWork, IGroupCallsRepository groupCallsRepository, UpdateFanout fanout, TimeProvider timeProvider,
        ILogger log)
    {
        _groupCallsRepository = groupCallsRepository;

        _options = options;
        _unitOfWork = unitOfWork;
        _fanout = fanout;
        _timeProvider = timeProvider;
        _log = log;
    }

    public long RefreshedCount => Interlocked.Read(ref _refreshed);

    public long ParticipantThrottledCount => Interlocked.Read(ref _throttledParticipant);

    public long CallThrottledCount => Interlocked.Read(ref _throttledCall);

    /// <summary>
    /// Per-participant then per-call rate limiting. Pure and lock-guarded so the
    /// bounds can be asserted without any repository or fan-out.
    /// </summary>
    public GroupCallActivityDecision Evaluate(long callId, long userId)
    {
        long nowTicks = _timeProvider.GetUtcNow().UtcTicks;
        lock (_gate)
        {
            var key = (callId, userId);
            if (_lastRefreshTicks.TryGetValue(key, out long lastTicks) &&
                nowTicks - lastTicks < _options.ParticipantInterval.Ticks)
            {
                Interlocked.Increment(ref _throttledParticipant);
                return GroupCallActivityDecision.ParticipantThrottled;
            }

            if (!_callWindows.TryGetValue(callId, out Queue<long>? window))
            {
                window = new Queue<long>();
                _callWindows[callId] = window;
            }
            long cutoff = nowTicks - _options.CallWindow.Ticks;
            while (window.Count > 0 && window.Peek() <= cutoff)
            {
                window.Dequeue();
            }
            if (window.Count >= _options.MaxRefreshesPerCallWindow)
            {
                Interlocked.Increment(ref _throttledCall);
                return GroupCallActivityDecision.CallThrottled;
            }

            window.Enqueue(nowTicks);
            TrimTrackedParticipants();
            _lastRefreshTicks[key] = nowTicks;
            Interlocked.Increment(ref _refreshed);
            return GroupCallActivityDecision.Refresh;
        }
    }

    /// <summary>
    /// Records one speaking report. When the rate limit allows it, touches the
    /// participant's active_date (a version-free repository mutation) and fans a
    /// min participant row out to the call's members. Min rows carry no
    /// viewer-local mute/volume, so each client keeps the state it already holds
    /// and applies only the fresh active_date. Returns the number of members the
    /// refresh reached; 0 when throttled or when the call/participant is gone.
    /// </summary>
    public async Task<int> ReportSpeakingAsync(long callId, long userId,
        CancellationToken cancellationToken = default)
    {
        if (Evaluate(callId, userId) != GroupCallActivityDecision.Refresh)
        {
            return 0;
        }

        using TLDto.TLGroupCallState? call = await _groupCallsRepository
            .GetCallAsync(callId, cancellationToken);
        if (call == null)
        {
            return 0;
        }
        var callState = call.Value.AsGroupCallState();
        if (callState.State != (int)GroupCallPersistenceState.Active)
        {
            return 0;
        }
        long peerChatId = callState.PeerId;
        int callVersion = callState.Version;

        int activeDate = (int)_timeProvider.GetUtcNow().ToUnixTimeSeconds();
        if (!await _groupCallsRepository.TryTouchParticipantActiveDateAsync(
                callId, userId, activeDate, cancellationToken))
        {
            return 0;
        }
        await _unitOfWork.SaveAsync();

        using TLDto.TLGroupCallParticipantState? participant = await _groupCallsRepository.GetParticipantAsync(callId, userId, cancellationToken);
        if (participant == null)
        {
            return 0;
        }

        // The call row is re-read above rather than re-derived: the version carried
        // here must be the call's CURRENT version, unchanged by this refresh, so a
        // client never treats telemetry as a versioned gap. The active_date is the
        // one just written rather than whatever the re-read returned, so a lagging
        // read can never publish a stale timestamp.
        using TLDto.TLGroupCallParticipantState refreshed = participant.Value
            .AsGroupCallParticipantState().Clone().ActiveDate(activeDate).Build();
        byte[] participantBytes;
        using (TLGroupCallParticipant row = GroupCallBuilders.BuildParticipant(
                   refreshed,
                   new GroupCallViewer(userId, CanManageCall: false,
                       ScheduleStartSubscribed: false),
                   GroupCallParticipantOverlay.None,
                   GroupCallParticipantDecoration.Min))
        {
            participantBytes = row.AsSpan().ToArray();
        }
        byte[] callBytes;
        using (TLInputGroupCall inputCall =
               GroupCallBuilders.BuildInputGroupCall(call.Value))
        {
            callBytes = inputCall.AsSpan().ToArray();
        }

        int delivered = await _fanout.PushGroupCallUpdatesAsync(peerChatId,
            excludeUserId: null, _ => Task.FromResult<TLUpdate?>(
                BuildActivityUpdate(callBytes, participantBytes, callVersion)));
        _log.Debug($"📞 GroupCall activity call:{callId} user:{userId} " +
                   $"members:{delivered} version:{callVersion}");
        return delivered;
    }

    // A participant row is dropped from a call's tracking when the call ends;
    // callers invoke this from the discard path so long-lived servers do not
    // accumulate state for calls that no longer exist.
    public void Forget(long callId)
    {
        lock (_gate)
        {
            _callWindows.Remove(callId);
            foreach (var key in _lastRefreshTicks.Keys.Where(k => k.CallId == callId)
                         .ToList())
            {
                _lastRefreshTicks.Remove(key);
            }
        }
    }

    private static TLUpdate BuildActivityUpdate(byte[] callBytes, byte[] participantBytes,
        int callVersion)
    {
        var participants = new Vector();
        participants.AppendTLObject(participantBytes);
        return UpdateGroupCallParticipants.Builder()
            .Call(callBytes)
            .Participants(participants)
            .Version(callVersion)
            .Build();
    }

    private void TrimTrackedParticipants()
    {
        if (_lastRefreshTicks.Count < _options.MaxTrackedParticipants)
        {
            return;
        }

        foreach (var key in _lastRefreshTicks.OrderBy(entry => entry.Value)
                     .Take(_lastRefreshTicks.Count - _options.MaxTrackedParticipants + 1)
                     .Select(entry => entry.Key).ToList())
        {
            _lastRefreshTicks.Remove(key);
        }
    }
}
