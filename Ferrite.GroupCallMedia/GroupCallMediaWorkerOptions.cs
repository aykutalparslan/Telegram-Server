// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;

namespace Ferrite.GroupCallMedia;

/// <summary>
/// Pinned identifiers for the mediasoup group-call worker control channel. The
/// worker version is accepted engine; the protocol version guards the
/// request/response contract in this adapter.
/// </summary>
public static class GroupCallMediaProtocol
{
    public const string Version = "1";
    public const string WorkerVersion = "3.21.2";
    public const string ProtocolHeader = "X-Ferrite-GroupCall-Protocol";
}

/// <summary>
/// Configuration for <see cref="MediasoupGroupCallMediaPlane"/>. The control URL
/// is a loopback/private authenticated endpoint; the secret is a bearer token.
/// <see cref="ToString"/> redacts the secret.
/// </summary>
public sealed record GroupCallMediaWorkerOptions
{
    public required Uri ControlUrl { get; init; }

    public required string AuthSecret { get; init; }

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan HealthTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Bounded retries for idempotent operations only.</summary>
    public int MaxRetries { get; init; } = 2;

    public TimeSpan RetryBackoff { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Delay before reconnecting the bounded NDJSON event stream.</summary>
    public TimeSpan EventReconnectBackoff { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Maximum bytes accepted for one worker event line.</summary>
    public int MaxEventBytes { get; init; } = 16 * 1024;

    public string ProtocolVersion { get; init; } = GroupCallMediaProtocol.Version;

    public string WorkerVersion { get; init; } = GroupCallMediaProtocol.WorkerVersion;

    public void Validate()
    {
        if (ControlUrl is null || !ControlUrl.IsAbsoluteUri ||
            (ControlUrl.Scheme != Uri.UriSchemeHttp && ControlUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "group-call media control URL must be an absolute http(s) URI");
        }
        if (string.IsNullOrEmpty(AuthSecret))
        {
            throw new ArgumentException("group-call media auth secret must not be empty");
        }
        if (RequestTimeout <= TimeSpan.Zero || HealthTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("group-call media timeouts must be positive");
        }
        if (MaxRetries < 0)
        {
            throw new ArgumentException("group-call media MaxRetries must not be negative");
        }
        if (RetryBackoff < TimeSpan.Zero || EventReconnectBackoff < TimeSpan.Zero)
        {
            throw new ArgumentException("group-call media backoffs must not be negative");
        }
        if (MaxEventBytes is < 256 or > 1024 * 1024)
        {
            throw new ArgumentException(
                "group-call media MaxEventBytes must be between 256 and 1048576");
        }
        if (string.IsNullOrEmpty(ProtocolVersion) || string.IsNullOrEmpty(WorkerVersion))
        {
            throw new ArgumentException("group-call media protocol/worker versions must be set");
        }
    }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("ControlUrl = ").Append(ControlUrl);
        builder.Append(", AuthSecret = <redacted>");
        builder.Append(", RequestTimeout = ").Append(RequestTimeout);
        builder.Append(", HealthTimeout = ").Append(HealthTimeout);
        builder.Append(", MaxRetries = ").Append(MaxRetries);
        builder.Append(", RetryBackoff = ").Append(RetryBackoff);
        builder.Append(", EventReconnectBackoff = ").Append(EventReconnectBackoff);
        builder.Append(", MaxEventBytes = ").Append(MaxEventBytes);
        builder.Append(", ProtocolVersion = ").Append(ProtocolVersion);
        builder.Append(", WorkerVersion = ").Append(WorkerVersion);
        return true;
    }
}
