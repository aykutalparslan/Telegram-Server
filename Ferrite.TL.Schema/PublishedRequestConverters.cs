// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

public static class PublishedRequestConverters
{
    public static IReadOnlyList<LayerRequestDefault> Defaults { get; } =
        Array.AsReadOnly(new[]
        {
            Default(150, 214, "autoDownloadSettings", "large_queue_active_operations_max", 0),
            Default(150, 214, "autoDownloadSettings", "small_queue_active_operations_max", 0),
            Default(150, 214, "help.getAppConfig", "hash", 0),
            Default(223, 224, "messages.getPollResults", "poll_hash", 0L),
            Default(224, 225, "inputStorePaymentAuthCode", "premium_days", 0)
        });

    public static IReadOnlyList<LayerRequestConverterDeclaration> Converters { get; } =
        Array.AsReadOnly(new[]
        {
            Constructor(150, 214, "dialogFilter", DialogFilterConverters.Upgrade),
            Constructor(150, 214, "documentAttributeVideo", VideoAttributeConverters.Upgrade),
            Constructor(150, 214, "emojiStatusUntil", ProfileConverters.UpgradeEmojiStatus),
            Constructor(150, 214, "globalPrivacySettings", PrivacyConverters.Upgrade),
            Constructor(150, 214, "invoice", InvoiceConverters.Upgrade),
            Constructor(150, 214, "poll", PollConverters.UpgradeQuestion),
            Constructor(150, 214, "pollAnswer", PollConverters.UpgradeAnswer),
            Method(150, 214, "channels.toggleForum", ForumTopicConverters.UpgradeToggle),
            Method(150, 214, "channels.toggleSignatures", ChannelConverters.UpgradeSignatures),
            Method(150, 214, "messages.prolongWebView", ReplyConverters.UpgradeRequest),
            Method(150, 214, "messages.report", ReportConverters.UpgradeRequest),
            Method(150, 214, "messages.requestWebView", ReplyConverters.UpgradeRequest),
            Method(150, 214, "messages.saveDraft", ReplyConverters.UpgradeRequest),
            Method(150, 214, "messages.sendInlineBotResult", ReplyConverters.UpgradeRequest),
            Method(150, 214, "messages.sendMedia", ReplyConverters.UpgradeRequest),
            Method(150, 214, "messages.sendMessage", ReplyConverters.UpgradeRequest),
            Method(150, 214, "messages.sendMultiMedia", ReplyConverters.UpgradeRequest),
            Method(150, 214, "messages.sendScreenshotNotification", ReplyConverters.UpgradeRequest),
            Method(150, 214, "messages.setChatTheme", ChatThemeConverters.UpgradeRequest),
            Method(150, 214, "messages.translateText", TextConverters.UpgradeTranslation),
            Method(150, 214, "stats.getMessagePublicForwards", StatisticsConverters.UpgradePublicForwards),
            Method(150, 214, "stickers.createStickerSet", StickerSetConverters.UpgradeCreate),
            Method(214, 215, "account.getUniqueGiftChatThemes", ChatThemeConverters.UpgradeGiftOffset),
            Method(215, 216, "account.updateColor", ProfileConverters.UpgradeColor),
            Method(215, 216, "channels.createForumTopic", ForumTopicConverters.UpgradeRequest),
            Method(215, 216, "channels.deleteTopicHistory", ForumTopicConverters.UpgradeRequest),
            Method(215, 216, "channels.editForumTopic", ForumTopicConverters.UpgradeRequest),
            Method(215, 216, "channels.getForumTopics", ForumTopicConverters.UpgradeRequest),
            Method(215, 216, "channels.getForumTopicsByID", ForumTopicConverters.UpgradeRequest),
            Method(215, 216, "channels.reorderPinnedForumTopics", ForumTopicConverters.UpgradeRequest),
            Method(215, 216, "channels.updatePinnedForumTopic", ForumTopicConverters.UpgradeRequest),
            Method(222, 223, "channels.editCreator", ChannelConverters.UpgradeCreator),
            Constructor(223, 224, "inputMediaPoll", PollConverters.UpgradeCorrectAnswers),
            Constructor(223, 224, "poll", PollConverters.UpgradeHash),
            Method(224, 225, "messages.composeMessageWithAI", TextConverters.UpgradeComposeTone),
            Constructor(228, 229, "inputKeyboardButtonRequestPeer", ReplyMarkupConverters.UpgradeButton),
            Constructor(228, 229, "inputKeyboardButtonUrlAuth", ReplyMarkupConverters.UpgradeButton),
            Constructor(228, 229, "inputKeyboardButtonUserProfile", ReplyMarkupConverters.UpgradeButton),
            Constructor(228, 229, "keyboardButton", ReplyMarkupConverters.UpgradeButton),
            Constructor(228, 229, "keyboardButtonBuy", ReplyMarkupConverters.UpgradeButton),
            Constructor(228, 229, "keyboardButtonCallback", ReplyMarkupConverters.UpgradeButton),
            Constructor(228, 229, "keyboardButtonCopy", ReplyMarkupConverters.UpgradeButton),
            Constructor(228, 229, "keyboardButtonGame", ReplyMarkupConverters.UpgradeButton),
            Constructor(228, 229, "keyboardButtonRequestGeoLocation", ReplyMarkupConverters.UpgradeButton),
            Constructor(228, 229, "keyboardButtonRequestPeer", ReplyMarkupConverters.UpgradeButton),
            Constructor(228, 229, "keyboardButtonRequestPhone", ReplyMarkupConverters.UpgradeButton),
            Constructor(228, 229, "keyboardButtonRequestPoll", ReplyMarkupConverters.UpgradeButton),
            Constructor(228, 229, "keyboardButtonSimpleWebView", ReplyMarkupConverters.UpgradeButton),
            Constructor(228, 229, "keyboardButtonSwitchInline", ReplyMarkupConverters.UpgradeButton),
            Constructor(228, 229, "keyboardButtonUrl", ReplyMarkupConverters.UpgradeButton),
            Constructor(228, 229, "keyboardButtonUrlAuth", ReplyMarkupConverters.UpgradeButton),
            Constructor(228, 229, "keyboardButtonUserProfile", ReplyMarkupConverters.UpgradeButton),
            Constructor(228, 229, "keyboardButtonWebView", ReplyMarkupConverters.UpgradeButton),
            Constructor(228, 229, "replyInlineMarkup", ReplyMarkupConverters.UpgradeInlineMarkup)
        });

    private static LayerRequestDefault Default(int source, int target, string owner,
        string field, object value)
    {
        return new LayerRequestDefault(new LayerEdge(source, target), owner, field, value);
    }

    private static LayerRequestConverterDeclaration Method(int source, int target,
        string name, LayerRequestConverter converter)
    {
        return new LayerRequestConverterDeclaration(new LayerEdge(source, target), true,
            name, converter);
    }

    private static LayerRequestConverterDeclaration Constructor(int source, int target,
        string name, LayerRequestConverter converter)
    {
        return new LayerRequestConverterDeclaration(new LayerEdge(source, target), false,
            name, converter);
    }
}
