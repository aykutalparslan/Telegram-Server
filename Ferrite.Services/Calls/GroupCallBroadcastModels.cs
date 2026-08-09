// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

public enum GroupCallBroadcastFailureKind
{
    Unavailable,
    Rejected,
    NotReady,
    Expired,
    Unsupported,
}

public sealed class GroupCallBroadcastException : Exception
{
    public GroupCallBroadcastException(GroupCallBroadcastFailureKind kind,
        string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public GroupCallBroadcastFailureKind Kind { get; }
}

public sealed record GroupCallBroadcastHealth(bool Healthy, int Streams,
    int LiveStreams, int Segments, long Bytes, string? FfmpegVersion);

public sealed record GroupCallBroadcastCredentials(string Url, string Key,
    int Generation);

public sealed record GroupCallBroadcastChannel(int Channel, int Scale,
    long LastTimestampMs);

public sealed record GroupCallBroadcastSegmentRequest(long CallId,
    long TimestampMs, int Scale, int Channel, int VideoQuality);

public sealed record GroupCallBroadcastOptions
{
    public bool EnableQualityLadder { get; init; }

    public int MaxSegmentBytes { get; init; } = 1024 * 1024;

    public TimeSpan HealthPollInterval { get; init; } = TimeSpan.FromSeconds(5);

    public void Validate()
    {
        if (MaxSegmentBytes is < 4096 or > 1024 * 1024)
        {
            throw new ArgumentException(
                "broadcast MaxSegmentBytes must be between 4096 and 1048576");
        }
        if (HealthPollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                "broadcast health poll interval must be positive");
        }
    }
}
