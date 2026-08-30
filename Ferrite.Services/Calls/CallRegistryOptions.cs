// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

public sealed class CallRegistryOptions
{
    public int MaxActiveCallsPerUser { get; init; } = 8;
    public int MaxTotalCalls { get; init; } = 65536;
    public int MaxRequestsPerWindow { get; init; } = 32;
    public TimeSpan RequestRateWindow { get; init; } = TimeSpan.FromMinutes(1);
    public int MaxProtocolVersions { get; init; } = 16;
    public int MaxVersionStringLength { get; init; } = 32;

    public int MaxSignalingDataBytes { get; init; } = 64 * 1024;
    public TimeSpan ReceiveDeadline { get; init; } = TimeSpan.FromSeconds(20);
    public TimeSpan RingDeadline { get; init; } = TimeSpan.FromSeconds(90);
    public TimeSpan TombstoneTtl { get; init; } = TimeSpan.FromMinutes(10);
}
