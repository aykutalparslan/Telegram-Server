// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.help;

namespace Ferrite.Services.Handlers.HelpMethods;

public sealed class GetTimezonesListHandler
{
    [TLFunction(Constructors.baseLayer_GetTimezonesList)]
    public ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetTimezonesList)q;
        Vector timezones = new();
        uint hash = 2166136261;
        var now = DateTimeOffset.UtcNow;
        foreach (var zone in TimeZoneInfo.GetSystemTimeZones().OrderBy(z => z.Id,
                     StringComparer.Ordinal))
        {
            var offset = (int)zone.GetUtcOffset(now).TotalSeconds;
            using var value = Timezone.Builder()
                .Id(Encoding.UTF8.GetBytes(zone.Id))
                .Name(Encoding.UTF8.GetBytes(zone.DisplayName))
                .UtcOffset(offset)
                .Build();
            timezones.AppendTLObject(value.ToReadOnlySpan());
            hash = AddToHash(hash, value.ToReadOnlySpan());
        }

        var resultHash = unchecked((int)hash);
        if (resultHash == 0)
        {
            resultHash = 1;
        }

        if (request.Hash == resultHash)
        {
            var notModified = TimezonesListNotModified.Builder().Build();
            return ValueTask.FromResult(notModified.TLBytes!.Value);
        }

        var result = TimezonesList.Builder()
            .Timezones(timezones)
            .Hash(resultHash)
            .Build();
        return ValueTask.FromResult(result.TLBytes!.Value);
    }

    private static uint AddToHash(uint hash, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            hash ^= value;
            hash *= 16777619;
        }
        return hash;
    }
}
