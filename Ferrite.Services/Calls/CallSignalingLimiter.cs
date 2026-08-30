// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

public sealed class CallSignalingLimiterOptions
{
    public int MaxPacketBytes { get; init; } = 64 * 1024;
    public int MaxPacketsPerWindow { get; init; } = 200;
    public TimeSpan Window { get; init; } = TimeSpan.FromSeconds(1);
}

public enum SignalingDecision
{
    Forward,
    TooLarge,
    RateLimited,
}

public sealed class CallSignalingLimiter
{
    private readonly CallSignalingLimiterOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private readonly Dictionary<long, Queue<long>> _windows = new();
    private long _forwarded;
    private long _droppedTooLarge;
    private long _droppedRate;

    public CallSignalingLimiter(CallSignalingLimiterOptions options,
        TimeProvider timeProvider)
    {
        _options = options;
        _timeProvider = timeProvider;
    }

    public long ForwardedCount => Interlocked.Read(ref _forwarded);

    public long DroppedTooLargeCount => Interlocked.Read(ref _droppedTooLarge);

    public long DroppedRateCount => Interlocked.Read(ref _droppedRate);

    public SignalingDecision Evaluate(long callId, int payloadLength)
    {
        if (payloadLength <= 0 || payloadLength > _options.MaxPacketBytes)
        {
            Interlocked.Increment(ref _droppedTooLarge);
            return SignalingDecision.TooLarge;
        }

        long nowTicks = _timeProvider.GetUtcNow().UtcTicks;
        long cutoff = nowTicks - _options.Window.Ticks;
        lock (_gate)
        {
            if (!_windows.TryGetValue(callId, out Queue<long>? window))
            {
                window = new Queue<long>();
                _windows[callId] = window;
            }

            while (window.Count > 0 && window.Peek() <= cutoff)
            {
                window.Dequeue();
            }

            if (window.Count >= _options.MaxPacketsPerWindow)
            {
                Interlocked.Increment(ref _droppedRate);
                return SignalingDecision.RateLimited;
            }

            window.Enqueue(nowTicks);
        }

        Interlocked.Increment(ref _forwarded);
        return SignalingDecision.Forward;
    }

    public void Remove(long callId)
    {
        lock (_gate)
        {
            _windows.Remove(callId);
        }
    }
}
