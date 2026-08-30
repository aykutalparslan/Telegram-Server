// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

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
