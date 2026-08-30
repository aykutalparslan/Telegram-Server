// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Utils;
public interface IMTProtoTime
{
    long FiveMinutesAgo { get; }
    long ThirtySecondsLater { get; }
    long GetUnixTimeInSeconds();
}
