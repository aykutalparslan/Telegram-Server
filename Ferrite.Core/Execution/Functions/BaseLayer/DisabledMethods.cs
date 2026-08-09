// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;

namespace Ferrite.Core.Execution.Functions.BaseLayer;

public static class DisabledMethods
{
    public static readonly FunctionKey[] Keys =
    [
        Key(unchecked((int)0x67a3ff2c)), // auth.importBotAuthorization
        Key(unchecked((int)0x66a08c7e)), // account.updateConnectedBot
        Key(unchecked((int)0x4ea4c80f)), // account.getConnectedBots
        Key(unchecked((int)0x76a86270)), // account.getBotBusinessConnection
        Key(unchecked((int)0x646e1097)), // account.toggleConnectedBotPaused
        Key(unchecked((int)0x5e437ed9)), // account.disablePeerConnectedBot
        Key(unchecked((int)0xb9d9a38d)), // account.toggleSponsoredMessages
        Key(unchecked((int)0xb6c8c393)), // contacts.getSponsoredPeers
        Key(unchecked((int)0xe6df7378)), // messages.startBot
        Key(unchecked((int)0x514e999d)), // messages.getInlineBotResults
        Key(unchecked((int)0xbb12a419)), // messages.setInlineBotResults
        Key(unchecked((int)0xc0cf7646)), // messages.sendInlineBotResult
        Key(unchecked((int)0x83557dba)), // messages.editInlineBotMessage
        Key(unchecked((int)0x9342ca07)), // messages.getBotCallbackAnswer
        Key(unchecked((int)0xd58f130a)), // messages.setBotCallbackAnswer
        Key(unchecked((int)0x15ad9f64)), // messages.setInlineGameScore
        Key(unchecked((int)0xf635e1b)), // messages.getInlineGameHighScores
        Key(unchecked((int)0xe5f672fa)), // messages.setBotShippingResults
        Key(unchecked((int)0x9c2dd95)), // messages.setBotPrecheckoutResults
        Key(unchecked((int)0x16fcc2cb)), // messages.getAttachMenuBots
        Key(unchecked((int)0x77216192)), // messages.getAttachMenuBot
        Key(unchecked((int)0x69f59d69)), // messages.toggleBotInAttachMenu
        Key(unchecked((int)0x269dc2c1)), // messages.requestWebView
        Key(unchecked((int)0xb0d81a83)), // messages.prolongWebView
        Key(unchecked((int)0x413a3e73)), // messages.requestSimpleWebView
        Key(unchecked((int)0xa4314f5)), // messages.sendWebViewResultMessage
        Key(unchecked((int)0xdc0242c8)), // messages.sendWebViewData
        Key(unchecked((int)0x91b2d060)), // messages.sendBotRequestedPeer
        Key(unchecked((int)0x34fdc5c3)), // messages.getBotApp
        Key(unchecked((int)0x53618bce)), // messages.requestAppWebView
        Key(unchecked((int)0xc9e01e7b)), // messages.requestMainWebView
        Key(unchecked((int)0x269e3643)), // messages.viewSponsoredMessage
        Key(unchecked((int)0x8235057e)), // messages.clickSponsoredMessage
        Key(unchecked((int)0x12cbf0c4)), // messages.reportSponsoredMessage
        Key(unchecked((int)0x3d6ce850)), // messages.getSponsoredMessages
        Key(unchecked((int)0xf21f7f2f)), // messages.savePreparedInlineMessage
        Key(unchecked((int)0x857ebdb8)), // messages.getPreparedInlineMessage
        Key(unchecked((int)0xec22cfcd)), // help.setBotUpdatesStatus
        Key(unchecked((int)0x9ae91519)), // channels.restrictSponsoredMessages
        Key(unchecked((int)0xaa2769ed)), // bots.sendCustomRequest
        Key(unchecked((int)0xe6213f4d)), // bots.answerWebhookJSONQuery
        Key(unchecked((int)0x517165a)), // bots.setBotCommands
        Key(unchecked((int)0x3d8de0f9)), // bots.resetBotCommands
        Key(unchecked((int)0xe34c0dd6)), // bots.getBotCommands
        Key(unchecked((int)0x4504d54f)), // bots.setBotMenuButton
        Key(unchecked((int)0x9c60eb28)), // bots.getBotMenuButton
        Key(unchecked((int)0x788464e1)), // bots.setBotBroadcastDefaultAdminRights
        Key(unchecked((int)0x925ec9ea)), // bots.setBotGroupDefaultAdminRights
        Key(unchecked((int)0x10cf3123)), // bots.setBotInfo
        Key(unchecked((int)0xdcd914fd)), // bots.getBotInfo
        Key(unchecked((int)0x9709b1c2)), // bots.reorderUsernames
        Key(unchecked((int)0x53ca973)), // bots.toggleUsername
        Key(unchecked((int)0x1359f4e6)), // bots.canSendMessage
        Key(unchecked((int)0xf132e3ef)), // bots.allowSendMessage
        Key(unchecked((int)0x87fc5e7)), // bots.invokeWebViewCustomMethod
        Key(unchecked((int)0xc2510192)), // bots.getPopularAppBots
        Key(unchecked((int)0x17aeb75a)), // bots.addPreviewMedia
        Key(unchecked((int)0x8525606f)), // bots.editPreviewMedia
        Key(unchecked((int)0x2d0135b3)), // bots.deletePreviewMedia
        Key(unchecked((int)0xb627f3aa)), // bots.reorderPreviewMedias
        Key(unchecked((int)0x423ab3ad)), // bots.getPreviewInfo
        Key(unchecked((int)0xa2a5594d)), // bots.getPreviewMedias
        Key(unchecked((int)0xed9f30c5)), // bots.updateUserEmojiStatus
        Key(unchecked((int)0x6de6392)), // bots.toggleUserEmojiStatusPermission
        Key(unchecked((int)0x50077589)), // bots.checkDownloadFileParams
        Key(unchecked((int)0xb0711d83)), // bots.getAdminedBots
        Key(unchecked((int)0x778b5ab3)), // bots.updateStarRefProgram
        Key(unchecked((int)0x8b89dfbd)), // bots.setCustomVerification
        Key(unchecked((int)0xa1b70815)), // bots.getBotRecommendations
        Key(unchecked((int)0x37148dbb)), // payments.getPaymentForm
        Key(unchecked((int)0x2478d1cc)), // payments.getPaymentReceipt
        Key(unchecked((int)0xb6c8f12b)), // payments.validateRequestedInfo
        Key(unchecked((int)0x2d03522f)), // payments.sendPaymentForm
        Key(unchecked((int)0x227d824b)), // payments.getSavedInfo
        Key(unchecked((int)0xd83d70c1)), // payments.clearSavedInfo
        Key(unchecked((int)0x2e79d779)), // payments.getBankCardData
        Key(unchecked((int)0xf91b065)), // payments.exportInvoice
        Key(unchecked((int)0x80ed747d)), // payments.assignAppStoreTransaction
        Key(unchecked((int)0xdffd50d3)), // payments.assignPlayMarketTransaction
        Key(unchecked((int)0x2757ba54)), // payments.getPremiumGiftCodeOptions
        Key(unchecked((int)0x8e51b4c1)), // payments.checkGiftCode
        Key(unchecked((int)0xf6e26854)), // payments.applyGiftCode
        Key(unchecked((int)0xf4239425)), // payments.getGiveawayInfo
        Key(unchecked((int)0x5ff58f20)), // payments.launchPrepaidGiveaway
        Key(unchecked((int)0xc00ec7d3)), // payments.getStarsTopupOptions
        Key(unchecked((int)0x4ea9b3bf)), // payments.getStarsStatus
        Key(unchecked((int)0x69da4557)), // payments.getStarsTransactions
        Key(unchecked((int)0x7998c914)), // payments.sendStarsForm
        Key(unchecked((int)0x25ae8f4a)), // payments.refundStarsCharge
        Key(unchecked((int)0xd91ffad6)), // payments.getStarsRevenueStats
        Key(unchecked((int)0x2433dc92)), // payments.getStarsRevenueWithdrawalUrl
        Key(unchecked((int)0xd1d7efc5)), // payments.getStarsRevenueAdsAccountUrl
        Key(unchecked((int)0x2dca16b8)), // payments.getStarsTransactionsByID
        Key(unchecked((int)0xd3c96bc8)), // payments.getStarsGiftOptions
        Key(unchecked((int)0x32512c5)), // payments.getStarsSubscriptions
        Key(unchecked((int)0xc7770878)), // payments.changeStarsSubscription
        Key(unchecked((int)0xcc5bebb3)), // payments.fulfillStarsSubscription
        Key(unchecked((int)0xbd1efd3e)), // payments.getStarsGiveawayOptions
        Key(unchecked((int)0xc4563590)), // payments.getStarGifts
        Key(unchecked((int)0x2a2a697c)), // payments.saveStarGift
        Key(unchecked((int)0x74bf076b)), // payments.convertStarGift
        Key(unchecked((int)0x6dfa0622)), // payments.botCancelStarsSubscription
        Key(unchecked((int)0x5869a553)), // payments.getConnectedStarRefBots
        Key(unchecked((int)0xb7d998f0)), // payments.getConnectedStarRefBot
        Key(unchecked((int)0xd6b48f7)), // payments.getSuggestedStarRefBots
        Key(unchecked((int)0x7ed5348a)), // payments.connectStarRefBot
        Key(unchecked((int)0xe4fca4a3)), // payments.editConnectedStarRefBot
        Key(unchecked((int)0x9c9abcb1)), // payments.getStarGiftUpgradePreview
        Key(unchecked((int)0xaed6e4f5)), // payments.upgradeStarGift
        Key(unchecked((int)0x7f18176a)), // payments.transferStarGift
        Key(unchecked((int)0xa1974d72)), // payments.getUniqueStarGift
        Key(unchecked((int)0xa319e569)), // payments.getSavedStarGifts
        Key(unchecked((int)0xb455a106)), // payments.getSavedStarGift
        Key(unchecked((int)0xd06e93a8)), // payments.getStarGiftWithdrawalUrl
        Key(unchecked((int)0x60eaefa1)), // payments.toggleChatStarGiftNotifications
        Key(unchecked((int)0x1513e7b0)), // payments.toggleStarGiftsPinnedToTop
        Key(unchecked((int)0x4fdc5ea7)), // payments.canPurchaseStore
        Key(unchecked((int)0x7a5fa236)), // payments.getResaleStarGifts
        Key(unchecked((int)0xedbe6ccb)), // payments.updateStarGiftPrice
        Key(unchecked((int)0x1f4a0e87)), // payments.createStarGiftCollection
        Key(unchecked((int)0x4fddbee7)), // payments.updateStarGiftCollection
        Key(unchecked((int)0xc32af4cc)), // payments.reorderStarGiftCollections
        Key(unchecked((int)0xad5648e8)), // payments.deleteStarGiftCollection
        Key(unchecked((int)0x981b91dd)), // payments.getStarGiftCollections
        Key(unchecked((int)0x4365af6b)), // payments.getUniqueStarGiftValueInfo
        Key(unchecked((int)0xc0c4edc9)), // payments.checkCanSendGift
        Key(unchecked((int)0x30eb63f0)), // stories.canSendStory
        Key(unchecked((int)0x737fc2ec)), // stories.sendStory
        Key(unchecked((int)0xb583ba46)), // stories.editStory
        Key(unchecked((int)0xae59db5f)), // stories.deleteStories
        Key(unchecked((int)0x9a75a1ef)), // stories.togglePinned
        Key(unchecked((int)0xeeb0d625)), // stories.getAllStories
        Key(unchecked((int)0x5821a5dc)), // stories.getPinnedStories
        Key(unchecked((int)0xb4352016)), // stories.getStoriesArchive
        Key(unchecked((int)0x5774ca74)), // stories.getStoriesByID
        Key(unchecked((int)0x7c2557c4)), // stories.toggleAllStoriesHidden
        Key(unchecked((int)0xa556dac8)), // stories.readStories
        Key(unchecked((int)0xb2028afb)), // stories.incrementStoryViews
        Key(unchecked((int)0x7ed23c57)), // stories.getStoryViewsList
        Key(unchecked((int)0x28e16cc8)), // stories.getStoriesViews
        Key(unchecked((int)0x7b8def20)), // stories.exportStoryLink
        Key(unchecked((int)0x19d8eb45)), // stories.report
        Key(unchecked((int)0x57bbd166)), // stories.activateStealthMode
        Key(unchecked((int)0x7fd736b2)), // stories.sendReaction
        Key(unchecked((int)0x2c4ada50)), // stories.getPeerStories
        Key(unchecked((int)0x9b5ae7f9)), // stories.getAllReadPeerStories
        Key(unchecked((int)0x535983c3)), // stories.getPeerMaxIDs
        Key(unchecked((int)0xa56a8b60)), // stories.getChatsToSend
        Key(unchecked((int)0xbd0415c4)), // stories.togglePeerStoriesHidden
        Key(unchecked((int)0xb9b2881f)), // stories.getStoryReactionsList
        Key(unchecked((int)0xb297e9b)), // stories.togglePinnedToTop
        Key(unchecked((int)0xd1810907)), // stories.searchPosts
        Key(unchecked((int)0xa36396e5)), // stories.createAlbum
        Key(unchecked((int)0x5e5259b6)), // stories.updateAlbum
        Key(unchecked((int)0x8535fbd9)), // stories.reorderAlbums
        Key(unchecked((int)0x8d3456d0)), // stories.deleteAlbum
        Key(unchecked((int)0x25b3eac7)), // stories.getAlbums
        Key(unchecked((int)0xac806d61)), // stories.getAlbumStories
        // Story STATISTICS follow the stories they describe. Both are keyed by a
        // story id, and every stories.* method above is disabled, so Ferrite
        // creates no story and no story id can ever resolve. They return to the
        // implement bucket only if stories are implemented.
        Key(unchecked((int)0x374fef40)), // stats.getStoryStats
        Key(unchecked((int)0xa6437ef6)), // stats.getStoryPublicForwards
        Key(unchecked((int)0x60f67660)), // premium.getBoostsList
        Key(unchecked((int)0xbe77b4a)), // premium.getMyBoosts
        Key(unchecked((int)0x6b7da746)), // premium.applyBoost
        Key(unchecked((int)0x42f1f61)), // premium.getBoostsStatus
        Key(unchecked((int)0x39854d1f)), // premium.getUserBoosts

        // business bots / chat links
        Key(unchecked((int)0xdd289f8e)), // invokeWithBusinessConnection

        // passport / secure values
        Key(unchecked((int)0xb288bc7d)), // account.getAllSecureValues
        Key(unchecked((int)0x73665bc2)), // account.getSecureValue
        Key(unchecked((int)0x899fe31d)), // account.saveSecureValue
        Key(unchecked((int)0xb880bc4b)), // account.deleteSecureValue
        Key(unchecked((int)0xa929597a)), // account.getAuthorizationForm
        Key(unchecked((int)0xf3ed4c73)), // account.acceptAuthorization

        // business bots / chat links
        Key(unchecked((int)0x4b00e066)), // account.updateBusinessWorkHours
        Key(unchecked((int)0x9e6b131a)), // account.updateBusinessLocation
        Key(unchecked((int)0x66cdafc4)), // account.updateBusinessGreetingMessage
        Key(unchecked((int)0xa26a7fa5)), // account.updateBusinessAwayMessage
        Key(unchecked((int)0xa614d034)), // account.updateBusinessIntro
        Key(unchecked((int)0x8851e68e)), // account.createBusinessChatLink
        Key(unchecked((int)0x8c3410af)), // account.editBusinessChatLink
        Key(unchecked((int)0x60073674)), // account.deleteBusinessChatLink
        Key(unchecked((int)0x6f70dde1)), // account.getBusinessChatLinks
        Key(unchecked((int)0x5492e5ee)), // account.resolveBusinessChatLink

        // paid reactions / paid messages / gifts
        Key(unchecked((int)0x19ba4a67)), // account.getPaidMessagesRevenue
        Key(unchecked((int)0xfe2eda76)), // account.toggleNoPaidMessagesException
        Key(unchecked((int)0xfe74ef9f)), // account.getUniqueGiftChatThemes

        // passport / secure values
        Key(unchecked((int)0x90c894b5)), // users.setSecureValueErrors

        // bot games
        Key(unchecked((int)0x8ef8ecc0)), // messages.setGameScore
        Key(unchecked((int)0xe822649d)), // messages.getGameHighScores

        // bot URL login
        Key(unchecked((int)0x198fb446)), // messages.requestUrlAuth
        Key(unchecked((int)0xb12c7125)), // messages.acceptUrlAuth

        // history import
        Key(unchecked((int)0x43fe19f3)), // messages.checkHistoryImport
        Key(unchecked((int)0x34090c3b)), // messages.initHistoryImport
        Key(unchecked((int)0x2a862092)), // messages.uploadImportedMedia
        Key(unchecked((int)0xb43df344)), // messages.startHistoryImport
        Key(unchecked((int)0x5dc60f03)), // messages.checkHistoryImportPeer

        // translation / transcription
        Key(unchecked((int)0x63183030)), // messages.translateText
        Key(unchecked((int)0x269e9a49)), // messages.transcribeAudio
        Key(unchecked((int)0x7f1d072f)), // messages.rateTranscribedAudio

        // paid reactions / paid messages / gifts
        Key(unchecked((int)0x84f80814)), // messages.getExtendedMedia

        // translation / transcription
        Key(unchecked((int)0xe47cb579)), // messages.togglePeerTranslations

        // saved reaction tags
        Key(unchecked((int)0x3637e05b)), // messages.getSavedReactionTags
        Key(unchecked((int)0x60297dec)), // messages.updateSavedReactionTag
        Key(unchecked((int)0xbdf93428)), // messages.getDefaultTagReactions

        // quick replies
        Key(unchecked((int)0xd483f2a8)), // messages.getQuickReplies
        Key(unchecked((int)0x60331907)), // messages.reorderQuickReplies
        Key(unchecked((int)0xf1d0fbd3)), // messages.checkQuickReplyShortcut
        Key(unchecked((int)0x5c003cef)), // messages.editQuickReplyShortcut
        Key(unchecked((int)0x3cc04740)), // messages.deleteQuickReplyShortcut
        Key(unchecked((int)0x94a495c3)), // messages.getQuickReplyMessages
        Key(unchecked((int)0x6c750de1)), // messages.sendQuickReplyMessages
        Key(unchecked((int)0xe105e910)), // messages.deleteQuickReplyMessages

        // fact check
        Key(unchecked((int)0x589ee75)), // messages.editFactCheck
        Key(unchecked((int)0xd1da940c)), // messages.deleteFactCheck
        Key(unchecked((int)0xb9cdc5ee)), // messages.getFactCheck

        // paid reactions / paid messages / gifts
        Key(unchecked((int)0x58bbcb50)), // messages.sendPaidReaction
        Key(unchecked((int)0x435885b5)), // messages.togglePaidReactionPrivacy
        Key(unchecked((int)0x472455aa)), // messages.getPaidReactionPrivacy

        // todo lists
        Key(unchecked((int)0xd3e03124)), // messages.toggleTodoCompleted
        Key(unchecked((int)0x21a61057)), // messages.appendTodoList

        // paid reactions / paid messages / gifts
        Key(unchecked((int)0x8107455c)), // messages.toggleSuggestedPostApproval

        // CDN / external web files
        Key(unchecked((int)0x24e6818d)), // upload.getWebFile
        Key(unchecked((int)0x395f69da)), // upload.getCdnFile
        Key(unchecked((int)0x9b2754a8)), // upload.reuploadCdnFile
        Key(unchecked((int)0x91dc3f31)), // upload.getCdnFileHashes

        // support surface (no support account in a self-hosted server)
        Key(unchecked((int)0x9cdf08cd)), // help.getSupport
        Key(unchecked((int)0xd360e72c)), // help.getSupportName
        Key(unchecked((int)0x38a08d3)), // help.getUserInfo
        Key(unchecked((int)0x66b91b70)), // help.editUserInfo

        // premium promo
        Key(unchecked((int)0xb81b93d4)), // help.getPremiumPromo

        // paid reactions / paid messages / gifts
        Key(unchecked((int)0x4b12327b)), // channels.updatePaidMessagesPrice

        // SMS jobs
        Key(unchecked((int)0xedc39d0)), // smsjobs.isEligibleToJoin
        Key(unchecked((int)0xa74ece2d)), // smsjobs.join
        Key(unchecked((int)0x9898ad73)), // smsjobs.leave
        Key(unchecked((int)0x93fa0bf)), // smsjobs.updateSettings
        Key(unchecked((int)0x10a698e8)), // smsjobs.getStatus
        Key(unchecked((int)0x778d902f)), // smsjobs.getSmsJob
        Key(unchecked((int)0x4f1ebf24)), // smsjobs.finishJob

        // Fragment collectibles
        Key(unchecked((int)0xbe1e85ba)), // fragment.getCollectibleInfo
    ];

    private static FunctionKey Key(int constructor) => new(TLFunctionAttribute.DefaultLayer, constructor);
}
