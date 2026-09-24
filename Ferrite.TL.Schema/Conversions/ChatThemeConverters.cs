// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Globalization;
using System.Text;

namespace Ferrite.TL.Schema;

internal static class ChatThemeConverters
{
    internal static LayerObjectValue UpgradeGiftOffset(LayerConversionContext context,
        LayerObjectValue source)
    {
        int offset = source.Get<int>("offset");
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["offset"] = Encoding.UTF8.GetBytes(offset == 0
                ? string.Empty
                : offset.ToString(CultureInfo.InvariantCulture))
        };
        return context.ConvertCompatible(source, "account.getUniqueGiftChatThemes",
            replacements);
    }

    internal static LayerObjectValue UpgradeRequest(LayerConversionContext context,
        LayerObjectValue source)
    {
        byte[] emoticon = source.Get<byte[]>("emoticon");
        var consumed = new HashSet<string>(StringComparer.Ordinal) { "emoticon" };
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["theme"] = emoticon.Length == 0
                ? context.Create("inputChatThemeEmpty")
                : context.Create("inputChatTheme",
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["emoticon"] = emoticon
                    })
        };
        return context.ConvertCompatible(source, "messages.setChatTheme", replacements,
            consumed);
    }

    internal static LayerObjectValue DowngradeAction(
        LayerConversionContext context, LayerObjectValue source)
    {
        if (!source.TryGetValue("theme", out object? value) ||
            !TryReadEmoticon(value, out byte[]? emoticon))
        {
            throw new InvalidOperationException(
                "The chat theme has no published form.");
        }
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["emoticon"] = emoticon
        };
        return context.ConvertCompatible(source, "messageActionSetChatTheme",
            replacements);
    }

    internal static LayerObjectValue DowngradeUserFull(LayerConversionContext context,
        LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (source.TryGetValue("theme", out object? value) &&
            TryReadEmoticon(value, out byte[]? emoticon))
        {
            replacements.Add("theme_emoticon", emoticon);
        }
        return context.ConvertCompatible(source, "userFull", replacements);
    }

    private static bool TryReadEmoticon(object? value, out byte[]? emoticon)
    {
        if (value is LayerObjectValue theme &&
            theme.Constructor.Name == "chatTheme" &&
            theme.TryGetValue("emoticon", out object? plain) &&
            plain is byte[] bytes)
        {
            emoticon = bytes;
            return true;
        }
        emoticon = null;
        return false;
    }
}
