// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

public static class PublishedLayerTransformRegistry
{
    public static LayerTransformRegistry Create()
    {
        var converters = new Dictionary<string, LayerSemanticConverter>(
            StringComparer.Ordinal)
        {
            ["convert_214_to_150_config"] = ConfigConverters.Downgrade,
            ["convert_214_to_150_dialogFilter"] = DialogFilterConverters.Downgrade,
            ["convert_214_to_150_documentAttributeVideo"] =
                VideoAttributeConverters.Downgrade,
            ["convert_214_to_150_draftMessage"] = ReplyConverters.DowngradeDraft,
            ["convert_214_to_150_globalPrivacySettings"] =
                PrivacyConverters.Downgrade,
            ["convert_214_to_150_messageActionSetChatTheme"] =
                ChatThemeConverters.DowngradeAction,
            ["convert_214_to_150_messageReplyHeader"] = ConvertCompatible,
            ["convert_214_to_150_messages_votesList"] = PollConverters.DowngradeVotesList,
            ["convert_214_to_150_poll"] = PollConverters.DowngradePoll,
            ["convert_214_to_150_pollAnswer"] = PollConverters.DowngradeAnswer,
            ["convert_214_to_150_pollResults"] = PollConverters.DowngradeResults,
            ["convert_214_to_150_reportResultReported"] = ReportConverters.DowngradeResult,
            ["convert_214_to_150_stats_broadcastStats"] =
                StatisticsConverters.DowngradeBroadcastStats,
            ["convert_214_to_150_stats_publicForwards"] =
                StatisticsConverters.DowngradePublicForwards,
            ["convert_214_to_150_stickerSet"] = ConvertCompatible,
            ["convert_214_to_150_stickerSetNoCovered"] = StickerSetConverters.DowngradeNoCoveredSet,
            ["convert_214_to_150_updateGroupCall"] = ConvertCompatible,
            ["convert_214_to_150_updatePeerBlocked"] = PrivacyConverters.DowngradePeerBlocked,
            ["convert_214_to_150_userFull"] = ChatThemeConverters.DowngradeUserFull,
            ["convert_216_to_215_updatePinnedForumTopic"] =
                ForumTopicConverters.DowngradePinnedTopic,
            ["convert_216_to_215_updatePinnedForumTopics"] =
                ForumTopicConverters.DowngradePinnedTopics,
            ["convert_217_to_216_channel"] = StoryConverters.DowngradeRecentMaximum,
            ["convert_217_to_216_updateGroupCall"] = GroupCallConverters.DowngradeUpdate,
            ["convert_217_to_216_user"] = StoryConverters.DowngradeRecentMaximum,
            ["convert_224_to_223_pollAnswerVoters"] = ConvertCompatible,
            ["convert_221_to_220_messageService"] = ConvertCompatible,
            ["convert_229_to_228_keyboardButton"] = ReplyMarkupConverters.DowngradeButton,
            ["convert_229_to_228_keyboardInlineButton"] = ReplyMarkupConverters.DowngradeInlineButton,
            ["convert_229_to_228_keyboardInlineButtonRow"] =
                ReplyMarkupConverters.DowngradeInlineRow,
            ["convert_229_to_228_replyInlineMarkup"] = ReplyMarkupConverters.DowngradeInlineMarkup
        };
        IReadOnlyList<ILayerTransformEdge> edges =
            LayerTransformEdgeCatalog.Create(converters);
        return new LayerTransformRegistry(PublishedLayerTransformRoute.All,
            edges);
    }

    private static LayerObjectValue ConvertCompatible(LayerConversionContext context,
        LayerObjectValue source)
    {
        return context.ConvertCompatible(source, source.Constructor.Name);
    }
}
