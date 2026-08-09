// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Net;
using System.Net.Sockets;

namespace Ferrite.Services.Calls;

/// <summary>
/// External self-hosted coturn STUN/TURN configuration. Ferrite only mints
/// short-lived REST credentials and advertises connection rows; bind and
/// external-IP mapping, relay port ranges, denied peer networks, quotas, and
/// bandwidth limits live in coturn's own configuration.
/// </summary>
public sealed record CallTurnOptions
{
    public bool Enabled { get; init; }

    /// <summary>Literal IPv4 address advertised in the row's ip field.</summary>
    public string AdvertisedIPv4 { get; init; } = "";

    /// <summary>Optional literal IPv6 address for the row's ipv6 field.</summary>
    public string AdvertisedIPv6 { get; init; } = "";

    public int Port { get; init; } = 3478;

    /// <summary>Operator realm; informational, not part of REST credentials.</summary>
    public string Realm { get; init; } = "";

    /// <summary>coturn static-auth-secret. Never logged or serialized.</summary>
    public string SharedSecret { get; init; } = "";

    public TimeSpan CredentialTtl { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Non-secret base for the two stable nonzero WebRTC connection ids
    /// (TURN row uses the seed, STUN row uses seed + 1).
    /// </summary>
    public long ConnectionIdSeed { get; init; } = 1_000_000;

    public bool TryValidate(out string error)
    {
        if (!Enabled)
        {
            error = "";
            return true;
        }

        if (string.IsNullOrEmpty(AdvertisedIPv4) &&
            string.IsNullOrEmpty(AdvertisedIPv6))
        {
            error = "coturn requires at least one advertised address";
            return false;
        }

        if (!string.IsNullOrEmpty(AdvertisedIPv4) &&
            (!IPAddress.TryParse(AdvertisedIPv4, out IPAddress? v4) ||
             v4.AddressFamily != AddressFamily.InterNetwork))
        {
            error = "coturn advertised IPv4 address is not a literal IPv4 address";
            return false;
        }

        if (!string.IsNullOrEmpty(AdvertisedIPv6) &&
            (!IPAddress.TryParse(AdvertisedIPv6, out IPAddress? v6) ||
             v6.AddressFamily != AddressFamily.InterNetworkV6))
        {
            error = "coturn advertised IPv6 address is not a literal IPv6 address";
            return false;
        }

        if (Port is < 1 or > ushort.MaxValue)
        {
            error = "coturn port is out of range";
            return false;
        }

        if (string.IsNullOrEmpty(SharedSecret))
        {
            error = "coturn shared secret is not configured";
            return false;
        }

        if (CredentialTtl <= TimeSpan.Zero)
        {
            error = "coturn credential TTL must be positive";
            return false;
        }

        if (ConnectionIdSeed <= 0)
        {
            error = "coturn connection id seed must be positive";
            return false;
        }

        error = "";
        return true;
    }

    /// <summary>Diagnostics never include the shared secret.</summary>
    public override string ToString() =>
        $"CallTurnOptions(enabled:{Enabled} ipv4:{AdvertisedIPv4} " +
        $"ipv6:{AdvertisedIPv6} port:{Port} realm:{Realm} " +
        $"ttl:{CredentialTtl} idSeed:{ConnectionIdSeed} secret:[redacted])";
}
