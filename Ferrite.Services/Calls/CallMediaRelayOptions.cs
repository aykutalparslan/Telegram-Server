// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Net;
using System.Net.Sockets;

namespace Ferrite.Services.Calls;

public sealed record CallMediaRelayOptions
{
    public string BindAddress { get; init; } = "0.0.0.0";

    public int BindPort { get; init; }

    public string AdvertisedAddress { get; init; } = "";

    public int AdvertisedPort { get; init; }

    public int MaxDatagramSize { get; init; } = 1560;

    public int MaxParticipantTagsPerAllocation { get; init; } = 8;

    public TimeSpan AllocationIdleTimeout { get; init; } = TimeSpan.FromMinutes(2);

    public TimeSpan IdleSweepInterval { get; init; } = TimeSpan.FromSeconds(30);

    public bool TryValidate(out string error)
    {
        if (!IPAddress.TryParse(BindAddress, out IPAddress? bind) ||
            bind.AddressFamily != AddressFamily.InterNetwork)
        {
            error = "call-media bind address is not a literal IPv4 address";
            return false;
        }

        if (BindPort is < 0 or > ushort.MaxValue)
        {
            error = "call-media bind port is out of range";
            return false;
        }

        if (!IPAddress.TryParse(AdvertisedAddress, out IPAddress? advertised) ||
            advertised.AddressFamily != AddressFamily.InterNetwork)
        {
            error = "call-media advertised address is not a literal IPv4 address";
            return false;
        }

        if (AdvertisedPort is < 0 or > ushort.MaxValue)
        {
            error = "call-media advertised port is out of range";
            return false;
        }

        if (MaxDatagramSize < 64)
        {
            error = "call-media max datagram size is too small";
            return false;
        }

        if (MaxParticipantTagsPerAllocation < 2)
        {
            error = "call-media participant tag bound must allow both sides";
            return false;
        }

        if (AllocationIdleTimeout <= TimeSpan.Zero ||
            IdleSweepInterval <= TimeSpan.Zero)
        {
            error = "call-media idle timeout and sweep interval must be positive";
            return false;
        }

        error = "";
        return true;
    }
}
