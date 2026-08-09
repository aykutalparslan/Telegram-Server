// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Utils;
/// <summary>
/// Helpers for getting the current UnixTime and approximate MTProto message ids
/// </summary>
public interface IMTProtoTime
{
    /// <summary>
    /// Returns a msg_id approximate 300 seconds in the past
    /// </summary>
    long FiveMinutesAgo { get; }
    /// <summary>
    /// Returns a msg_id approximate 30 seconds in the future
    /// </summary>
    long ThirtySecondsLater { get; }
    long GetUnixTimeInSeconds();
}
