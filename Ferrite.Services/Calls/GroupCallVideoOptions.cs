// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

/// <summary>
/// Video capability limits for group calls. The limit is a Ferrite capacity
/// choice tuned against media-worker capacity, not a protocol constant: neither
/// the schema nor the pinned client pins a value.
/// </summary>
public sealed record GroupCallVideoOptions(int UnmutedVideoLimit = 30)
{
    public void Validate()
    {
        if (UnmutedVideoLimit is < 1 or > 10_000)
        {
            throw new ArgumentException(
                "group-call unmuted video limit must be between 1 and 10000");
        }
    }
}
