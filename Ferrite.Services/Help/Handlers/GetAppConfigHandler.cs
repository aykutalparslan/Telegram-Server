// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.help;

namespace Ferrite.Services.Handlers.HelpMethods;

public sealed class GetAppConfigHandler
{
    private const int ConfigHash = 2;

    [TLFunction(Constructors.baseLayer_GetAppConfig)]
    public ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = new GetAppConfig(q.AsSpan());
        if (request.Hash == ConfigHash)
        {
            var unchanged = AppConfigNotModified.Builder().Build();
            return ValueTask.FromResult(unchanged.TLBytes!.Value);
        }

        var values = new Vector();
        AppendNumber(ref values, "telegram_antispam_group_size_min"u8, 1);
        AppendNumber(ref values, "hidden_members_group_size_min"u8, 1);
        AppendNumber(ref values, "channel_autotranslation_level_min"u8, 0);
        using var config = JsonObject.Builder().Value(values).Build();
        var result = AppConfig.Builder().Hash(ConfigHash)
            .Config(config.ToReadOnlySpan()).Build();
        return ValueTask.FromResult(result.TLBytes!.Value);
    }

    private static void AppendNumber(ref Vector values, ReadOnlySpan<byte> key,
        double value)
    {
        using var number = JsonNumber.Builder().Value(value).Build();
        using var item = JsonObjectValue.Builder().Key(key)
            .Value(number.ToReadOnlySpan()).Build();
        values.AppendTLObject(item.ToReadOnlySpan());
    }
}
