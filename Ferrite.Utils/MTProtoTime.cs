// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
namespace Ferrite.Utils;

/// <summary>
/// Calculates msg_id values 30 seconds in the future and
/// 300 seconds in the past periodically.
/// </summary>
public class MTProtoTime : IMTProtoTime
{
    //private static Lazy<MTProtoTime> _instance = new Lazy<MTProtoTime>(
    //    () => new MTProtoTime(),
    //    LazyThreadSafetyMode.ExecutionAndPublication);

    private long _seconds;
    private long _fiveMinutesAgo;
    /// <summary>
    /// Returns a msg_id approximate 300 seconds in the past
    /// </summary>
    public long FiveMinutesAgo => _fiveMinutesAgo;
    private long _thirtySecondsLater;
    /// <summary>
    /// Returns a msg_id approximate 30 seconds in the future
    /// </summary>
    public long ThirtySecondsLater => _thirtySecondsLater;
    private readonly Task _keepTimeTask;
    private async Task KeepTime()
    {
        while (true)
        {
            _seconds = DateTimeOffset.Now.ToUnixTimeSeconds();
            _fiveMinutesAgo = (_seconds - 300) * 4294967296L;
            _thirtySecondsLater = (_seconds + 30) * 4294967296L;
            await Task.Delay(1000);
        }
    }

    public long GetUnixTimeInSeconds()
    {
        return DateTimeOffset.Now.ToUnixTimeSeconds();
    }

    /// <summary>
    /// Fully thread safe; uses locking to ensure that only one thread initializes the value.
    /// </summary>
    //public static MTProtoTime Instance => _instance.Value;
    public MTProtoTime()
    {
        _keepTimeTask = KeepTime();
    }
}

