// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

internal static class TextConverters
{
    internal static LayerObjectValue UpgradeComposeTone(LayerConversionContext context,
        LayerObjectValue source)
    {
        var consumed = new HashSet<string>(StringComparer.Ordinal) { "change_tone" };
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tone"] = source.TryGetValue("change_tone", out object? tone)
                ? context.Create("inputAiComposeToneDefault",
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["tone"] = tone
                    })
                : null
        };
        return context.ConvertCompatible(source, "messages.composeMessageWithAI",
            replacements, consumed);
    }

    internal static LayerObjectValue UpgradeTranslation(LayerConversionContext context,
        LayerObjectValue source)
    {
        var consumed = new HashSet<string>(StringComparer.Ordinal)
        {
            "msg_id",
            "from_lang"
        };
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = source.TryGetValue("msg_id", out object? messageId)
                ? Array.AsReadOnly(new[] { messageId })
                : null,
            ["text"] = source.Fields.ContainsKey("text")
                ? Array.AsReadOnly(new object?[]
                {
                    WithEntities(context, source, "text")
                })
                : null
        };
        return context.ConvertCompatible(source, "messages.translateText", replacements,
            consumed);
    }

    internal static LayerObjectValue WithEntities(LayerConversionContext context,
        LayerObjectValue source, string field)
    {
        return context.Create("textWithEntities",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["text"] = source.Get<byte[]>(field),
                ["entities"] = Array.Empty<object?>()
            });
    }

    internal static byte[] PlainText(LayerObjectValue source, string field)
    {
        if (!source.TryGetValue(field, out object? value) ||
            value is not LayerObjectValue text ||
            text.Constructor.Name != "textWithEntities" ||
            !text.TryGetValue("text", out object? plain) ||
            plain is not byte[] bytes)
        {
            throw new InvalidOperationException("The field " + field +
                " has no plain-text form.");
        }
        return bytes;
    }
}
