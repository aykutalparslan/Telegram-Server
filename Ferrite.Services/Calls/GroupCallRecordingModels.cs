// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

public enum GroupCallRecordingFailureKind
{
    Unavailable,
    Rejected,
    NotFound,
    Conflict,
    LimitExceeded,
    InvalidResponse,
}

public sealed class GroupCallRecordingException : Exception
{
    public GroupCallRecordingException(GroupCallRecordingFailureKind kind,
        string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public GroupCallRecordingFailureKind Kind { get; }
}

public sealed record GroupCallRecordingRequest(long CallId, int Generation,
    int StartedDate, long InitiatingUserId, string Title, bool Video,
    bool Portrait);

public sealed record GroupCallRecordingHealth(bool Healthy, int ActiveRecordings,
    int FinalizedRecordings, long Bytes, string? FfmpegVersion);

/// <summary>
/// One owned, bounded finalized recording. The stream stays valid until this
/// value is disposed; HTTP implementations use the callback to release the
/// response and its linked timeout only after the importer has consumed it.
/// </summary>
public sealed class GroupCallRecordingFile : IAsyncDisposable
{
    private readonly Func<ValueTask>? _dispose;
    private bool _disposed;

    public GroupCallRecordingFile(Stream content, long contentLength,
        string fileName, string mimeType, double durationSeconds, int width,
        int height, Func<ValueTask>? dispose = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (contentLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(contentLength));
        }
        if (string.IsNullOrWhiteSpace(fileName) ||
            string.IsNullOrWhiteSpace(mimeType))
        {
            throw new ArgumentException("recording filename and MIME type are required");
        }
        if (durationSeconds < 0 || width < 0 || height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        }

        Content = content;
        ContentLength = contentLength;
        FileName = fileName;
        MimeType = mimeType;
        DurationSeconds = durationSeconds;
        Width = width;
        Height = height;
        _dispose = dispose;
    }

    public Stream Content { get; }

    public long ContentLength { get; }

    public string FileName { get; }

    public string MimeType { get; }

    public double DurationSeconds { get; }

    public int Width { get; }

    public int Height { get; }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        await Content.DisposeAsync();
        if (_dispose != null)
        {
            await _dispose();
        }
    }
}

public sealed record GroupCallRecordingOptions
{
    public const long UploadMaximumBytes = (long)4000 * 512 * 1024;

    public long MaxRecordingBytes { get; init; } = UploadMaximumBytes;

    public TimeSpan FinalizeTimeout { get; init; } = TimeSpan.FromMinutes(2);

    public TimeSpan HealthPollInterval { get; init; } = TimeSpan.FromSeconds(5);

    public int MaxTitleBytes { get; init; } = 255;

    public void Validate()
    {
        if (MaxRecordingBytes is < 1024 or > UploadMaximumBytes)
        {
            throw new ArgumentException(
                $"recording MaxRecordingBytes must be between 1024 and {UploadMaximumBytes}");
        }
        if (FinalizeTimeout <= TimeSpan.Zero || HealthPollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentException("recording timeouts must be positive");
        }
        if (MaxTitleBytes is < 1 or > 1024)
        {
            throw new ArgumentException(
                "recording MaxTitleBytes must be between 1 and 1024");
        }
    }
}
