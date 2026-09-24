// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

internal static class ReplyMarkupConverters
{
    internal static LayerObjectValue UpgradeButton(LayerConversionContext context,
        LayerObjectValue source)
    {
        string name = source.Constructor.Name;
        string type = name == "keyboardButton"
            ? "buttonTypeDefault"
            : ButtonTypeName(name, "buttonType");
        return SplitButton(context, source, "keyboardButton", type);
    }

    internal static LayerObjectValue UpgradeInlineMarkup(LayerConversionContext context,
        LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["rows"] = context.ConvertElements(source, "rows", UpgradeInlineRow)
        };
        return context.ConvertCompatible(source, "replyInlineMarkup", replacements);
    }

    private static LayerObjectValue UpgradeInlineRow(LayerConversionContext context,
        LayerObjectValue source)
    {
        return context.Create("keyboardInlineButtonRow",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["buttons"] =
                    context.ConvertElements(source, "buttons", UpgradeInlineButton)
            });
    }

    private static LayerObjectValue UpgradeInlineButton(LayerConversionContext context,
        LayerObjectValue source)
    {
        return SplitButton(context, source, "keyboardInlineButton",
            ButtonTypeName(source.Constructor.Name, "inlineButtonType"));
    }

    private static LayerObjectValue SplitButton(LayerConversionContext context,
        LayerObjectValue source, string button, string type)
    {
        string[] shared = ["style", "text"];
        LayerObjectValue value = context.ConvertCompatible(source, type, null, shared);
        string[] carried = source.Constructor.Fields.Select(x => x.Name)
            .Where(x => !shared.Contains(x)).ToArray();
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = value
        };
        return context.ConvertCompatible(source, button, replacements, carried);
    }

    private static string ButtonTypeName(string button, string type)
    {
        const string input = "inputKeyboardButton";
        const string plain = "keyboardButton";
        if (button.StartsWith(input, StringComparison.Ordinal))
        {
            return "input" + char.ToUpperInvariant(type[0]) + type.Substring(1) +
                   button.Substring(input.Length);
        }
        if (button.StartsWith(plain, StringComparison.Ordinal) &&
            button.Length > plain.Length)
        {
            return type + button.Substring(plain.Length);
        }
        throw new InvalidOperationException("The button " + button +
                                            " has no " + type + " form.");
    }

    internal static LayerObjectValue DowngradeButton(
        LayerConversionContext context, LayerObjectValue source)
    {
        if (!source.TryGetValue("type", out object? value) ||
            value is not LayerObjectValue type)
        {
            throw new InvalidOperationException(
                "The keyboard button has no type.");
        }
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal);
        string target;
        switch (type.Constructor.Name)
        {
            case "buttonTypeDefault":
                target = "keyboardButton";
                break;
            case "buttonTypeRequestPhone":
                target = "keyboardButtonRequestPhone";
                break;
            case "buttonTypeRequestGeoLocation":
                target = "keyboardButtonRequestGeoLocation";
                break;
            case "buttonTypeRequestPoll":
                target = "keyboardButtonRequestPoll";
                context.CopyField(type, replacements, "quiz");
                break;
            case "buttonTypeSimpleWebView":
                target = "keyboardButtonSimpleWebView";
                context.CopyField(type, replacements, "url");
                break;
            case "buttonTypeRequestPeer":
                target = "keyboardButtonRequestPeer";
                context.CopyField(type, replacements, "button_id");
                context.CopyField(type, replacements, "peer_type");
                context.CopyField(type, replacements, "max_quantity");
                break;
            case "inputButtonTypeRequestPeer":
                target = "inputKeyboardButtonRequestPeer";
                context.CopyField(type, replacements, "name_requested");
                context.CopyField(type, replacements, "username_requested");
                context.CopyField(type, replacements, "photo_requested");
                context.CopyField(type, replacements, "button_id");
                context.CopyField(type, replacements, "peer_type");
                context.CopyField(type, replacements, "max_quantity");
                break;
            default:
                throw new InvalidOperationException("The keyboard button type " +
                    type.Constructor.Name + " has no published form.");
        }
        return context.ConvertCompatible(source, target, replacements);
    }

    internal static LayerObjectValue DowngradeInlineMarkup(
        LayerConversionContext context, LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["rows"] = context.ConvertElements(source, "rows", DowngradeInlineRow)
        };
        return context.ConvertCompatible(source, "replyInlineMarkup",
            replacements);
    }

    internal static LayerObjectValue DowngradeInlineRow(
        LayerConversionContext context, LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["buttons"] =
                context.ConvertElements(source, "buttons", DowngradeInlineButton)
        };
        return context.ConvertCompatible(source, "keyboardButtonRow",
            replacements);
    }

    internal static LayerObjectValue DowngradeInlineButton(
        LayerConversionContext context, LayerObjectValue source)
    {
        if (!source.TryGetValue("type", out object? value) ||
            value is not LayerObjectValue type)
        {
            throw new InvalidOperationException(
                "The inline keyboard button has no type.");
        }
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal);
        string target;
        switch (type.Constructor.Name)
        {
            case "inlineButtonTypeUrl":
                target = "keyboardButtonUrl";
                context.CopyField(type, replacements, "url");
                break;
            case "inlineButtonTypeWebView":
                target = "keyboardButtonWebView";
                context.CopyField(type, replacements, "url");
                break;
            case "inlineButtonTypeCallback":
                target = "keyboardButtonCallback";
                context.CopyField(type, replacements, "requires_password");
                context.CopyField(type, replacements, "data");
                break;
            case "inlineButtonTypeGame":
                target = "keyboardButtonGame";
                break;
            case "inlineButtonTypeBuy":
                target = "keyboardButtonBuy";
                break;
            case "inlineButtonTypeCopy":
                target = "keyboardButtonCopy";
                context.CopyField(type, replacements, "copy_text");
                break;
            case "inlineButtonTypeUserProfile":
                target = "keyboardButtonUserProfile";
                context.CopyField(type, replacements, "user_id");
                break;
            case "inputInlineButtonTypeUserProfile":
                target = "inputKeyboardButtonUserProfile";
                context.CopyField(type, replacements, "user_id");
                break;
            case "inlineButtonTypeUrlAuth":
                target = "keyboardButtonUrlAuth";
                context.CopyField(type, replacements, "fwd_text");
                context.CopyField(type, replacements, "url");
                context.CopyField(type, replacements, "button_id");
                break;
            case "inputInlineButtonTypeUrlAuth":
                target = "inputKeyboardButtonUrlAuth";
                context.CopyField(type, replacements, "request_write_access");
                context.CopyField(type, replacements, "fwd_text");
                context.CopyField(type, replacements, "url");
                if (!type.Fields.ContainsKey("bot"))
                {
                    throw new InvalidOperationException(
                        "The url-auth button has no bot.");
                }
                context.CopyField(type, replacements, "bot");
                break;
            case "inlineButtonTypeSwitchInline":
                target = "keyboardButtonSwitchInline";
                context.CopyField(type, replacements, "same_peer");
                context.CopyField(type, replacements, "query");
                context.CopyField(type, replacements, "peer_types");
                break;
            default:
                throw new InvalidOperationException("The inline button type " +
                    type.Constructor.Name + " has no published form.");
        }
        return context.ConvertCompatible(source, target, replacements);
    }
}
