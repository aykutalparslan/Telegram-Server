// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
namespace Ferrite.Utils;

public class MTProtoTime : IMTProtoTime
{

    private long _seconds;
    private long _fiveMinutesAgo;
    public long FiveMinutesAgo => _fiveMinutesAgo;
    private long _thirtySecondsLater;
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

    public MTProtoTime()
    {
        _keepTimeTask = KeepTime();
    }
}

