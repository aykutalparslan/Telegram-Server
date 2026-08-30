// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.Services.Calls;
using Ferrite.Services.Channels;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class GetFullChannelHandler : ChannelsHandlerBase
{
    private readonly IChannelAdminRepository _channelAdminRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatInvitesRepository _chatInvitesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IForumTopicsRepository _forumTopicsRepository;
    private readonly IStickerRepository _stickerRepository;

    public GetFullChannelHandler(IUnitOfWork unitOfWork, IChannelAdminRepository channelAdminRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelMessagesRepository channelMessagesRepository, IChatInvitesRepository chatInvitesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IForumTopicsRepository forumTopicsRepository, IMessageRepository messageRepository, IStickerRepository stickerRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout, GroupCallChatLink groupCallLink,
        ChatSettingsStore settings)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _channelAdminRepository = channelAdminRepository;

        _authorizationRepository = authorizationRepository;
        _channelMessagesRepository = channelMessagesRepository;
        _chatInvitesRepository = chatInvitesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _forumTopicsRepository = forumTopicsRepository;
        _stickerRepository = stickerRepository;

        _groupCallLink = groupCallLink;
        _settings = settings;
    }

    private readonly GroupCallChatLink _groupCallLink;
    private readonly ChatSettingsStore _settings;

    [TLFunction(Constructors.baseLayer_GetFullChannel)]
    public async Task<Ferrite.TL.baseLayer.messages.TLChatFull> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (Ferrite.TL.baseLayer.messages.TLChatFull)RpcErrorGenerator
                .GenerateError(400, "AUTH_KEY_INVALID"u8);
        }

        long currentUserId = auth.Value.AsAuthInfo().UserId;
        long? channelId = ResolveInputChannelId(((GetFullChannel)q).Get_ChannelView());
        if (channelId == null)
        {
            return (Ferrite.TL.baseLayer.messages.TLChatFull)RpcErrorGenerator
                .GenerateError(400, "CHANNEL_INVALID"u8);
        }

        using var chat = await _chatRepository.GetChatAsync(channelId.Value);
        if (chat == null || chat.Value.Type != TLChat.ChatType.Channel)
        {
            return (Ferrite.TL.baseLayer.messages.TLChatFull)RpcErrorGenerator
                .GenerateError(400, "CHANNEL_INVALID"u8);
        }
        bool isMegagroup = chat.Value.AsChannel().Megagroup;

        var participantInfos = await _chatParticipantsRepository
            .GetParticipantsAsync(channelId.Value);
        var activeParticipants = participantInfos.Where(IsActiveParticipant).ToList();

        byte[] about = Array.Empty<byte>();
        int pinnedMsgId = 0;
        long migratedFromChatId = 0;
        int migratedFromMaxId = 0;
        byte[]? availableReactionsBytes = null;
        int reactionsLimit = 0;
        using var storedFullInfo = await _chatRepository.GetFullInfoAsync(channelId.Value);
        if (storedFullInfo != null)
        {
            var info = storedFullInfo.Value.AsChatFullInfo();
            about = info.About.ToArray();
            pinnedMsgId = info.PinnedMsgId;
            migratedFromChatId = info.MigratedFromChatId;
            migratedFromMaxId = info.MigratedFromMaxId;
            if (info.Flags[2])
            {
                availableReactionsBytes = info.AvailableReactions.ToArray();
            }
            reactionsLimit = info.ReactionsLimit;
        }

        if (migratedFromChatId > 0)
        {
            int viewerBoundary = await FindMigrationBoundaryMessageId(currentUserId,
                migratedFromChatId, channelId.Value);
            if (viewerBoundary > 0)
            {
                migratedFromMaxId = viewerBoundary;
            }
        }

        byte[]? migratedFromChatBytes = null;
        if (migratedFromChatId > 0)
        {
            using var migratedFromChat = await _chatRepository
                .GetChatAsync(migratedFromChatId);
            if (migratedFromChat != null)
            {
                migratedFromChatBytes = migratedFromChat.Value.AsSpan().ToArray();
            }
        }

        int readInboxMaxId = 0;
        int readOutboxMaxId = 0;
        using var readState = await _channelMessagesRepository
            .GetReadStateAsync(currentUserId, channelId.Value);
        if (readState != null)
        {
            var state = readState.Value.AsChannelReadState();
            readInboxMaxId = state.ReadInboxMaxId;
            readOutboxMaxId = state.ReadOutboxMaxId;
        }

        var channelBox = new ChannelMessageBox(_counterFactory, channelId.Value);
        int pts = await channelBox.Pts();

        byte[]? exportedInviteBytes = null;
        bool callerIsActiveMember = false;
        bool callerIsCreator = false;
        bool callerManagesInvites = false;
        bool callerCanSetStickers = false;
        bool callerCanChangeInfo = false;
        bool callerIsAdmin = false;
        foreach (var participantInfo in activeParticipants)
        {
            if (participantInfo.AsChatParticipantInfo().UserId != currentUserId)
            {
                continue;
            }
            callerIsActiveMember = true;
            callerIsCreator = participantInfo.AsChatParticipantInfo().Role ==
                (int)ChatParticipantRole.Creator;
            callerIsAdmin = ChatRights.HasAdminRight(participantInfo,
                ChatAdminRightRequirement.Any);
            callerCanChangeInfo = ChatRights.HasAdminRight(participantInfo,
                ChatAdminRightRequirement.ChangeInfo);
            callerCanSetStickers = isMegagroup && callerCanChangeInfo;
            if (ChatRights.HasAdminRight(participantInfo, ChatAdminRightRequirement.InviteUsers))
            {
                callerManagesInvites = true;
                exportedInviteBytes = await ChatInvites.GetPermanentInviteBytesAsync(
                    _chatInvitesRepository, channelId.Value);
            }
            break;
        }
        List<PendingInviteImporter> pendingRequests = callerManagesInvites
            ? await ChatInvites.GetPendingImportersAsync(
                _chatInvitesRepository, channelId.Value)
            : new List<PendingInviteImporter>();

        _log.Debug($"📣 GetFullChannel user:{currentUserId} channel:{channelId.Value} " +
                   $"participants:{activeParticipants.Count} pts:{pts}");

        bool viewForumAsMessages;
        var forumTopicsRepository = _forumTopicsRepository;
        if (forumTopicsRepository == null)
        {
            viewForumAsMessages = false;
        }
        else
        {
            using var forumUserState = await forumTopicsRepository
                .GetUserStateAsync(channelId.Value, currentUserId);
            viewForumAsMessages = forumUserState?.AsForumUserState()
                .ViewForumAsMessages == true;
        }

        using GroupCallFullLink callLink = await _groupCallLink.ResolveFullLinkAsync(
            GroupCallPeerType.Channel, channelId.Value, currentUserId);
        ChatSettingsSnapshot channelSettings = await _settings.GetAsync(
            ChatSettingsScope.ForChannel(channelId.Value));
        DialogPeerKey? defaultSendAs = await _settings.GetDefaultSendAsAsync(
            currentUserId,
            new DialogPeerKey(TLPeer.PeerType.PeerChannel, channelId.Value));
        using TLPeerWallpaper? wallpaper = await _settings
            .GetChannelWallpaperAsync(channelId.Value);
        long stickerSetId = 0;
        long emojiSetId = 0;
        using (TLChannelStickerState? association = await _stickerRepository.GetChannelStateAsync(channelId.Value))
        {
            if (association is not null)
            {
                var associationView = association.Value.AsChannelStickerState();
                stickerSetId = associationView.StickerSetId;
                emojiSetId = associationView.EmojiSetId;
            }
        }
        using TLStickerSetState? stickerSet = stickerSetId > 0
            ? await _stickerRepository.GetSetAsync(stickerSetId)
            : null;
        using TLStickerSetState? emojiSet = emojiSetId > 0
            ? await _stickerRepository.GetSetAsync(emojiSetId)
            : null;

        bool hiddenPrehistory;
        bool antispam;
        bool participantsHidden;
        bool statisticsAvailable;
        int statsDc;
        int slowmodeSeconds;
        int boostsUnrestrict;
        long linkedChatId;
        byte[]? locationBytes;
        byte[]? mainTabBytes;
        TLChannelAdminState? storedAdminState = await _channelAdminRepository.GetStateAsync(channelId.Value);
        using (TLChannelAdminState adminState = storedAdminState ??
                   ChannelAdminStateRows.Empty(channelId.Value, 0))
        {
            var adminView = adminState.AsChannelAdminState();
            hiddenPrehistory = adminView.HiddenPrehistory;
            antispam = adminView.Antispam;
            participantsHidden = adminView.ParticipantsHidden;
            statisticsAvailable = adminView.CanViewStats;
            statsDc = adminView.StatsDc;
            slowmodeSeconds = adminView.SlowmodeSeconds;
            boostsUnrestrict = adminView.BoostsUnrestrict;
            linkedChatId = adminView.LinkedChatId;
            locationBytes = adminView.Flags[4]
                ? adminView.Location.ToArray()
                : null;
            mainTabBytes = adminView.Flags[6]
                ? adminView.MainTab.ToArray()
                : null;
        }
        int slowmodeNextSendDate = 0;
        if (slowmodeSeconds > 0)
        {
            using TLChannelSlowModeState? deadline = await _channelAdminRepository
                .GetSlowModeStateAsync(channelId.Value, currentUserId);
            int nextSendDate = deadline?.AsChannelSlowModeState().NextSendDate ?? 0;
            if (nextSendDate > (int)DateTimeOffset.Now.ToUnixTimeSeconds())
            {
                slowmodeNextSendDate = nextSendDate;
            }
        }

        using var notifySettings = PeerNotifySettings.Builder().Build();
        using var emptyPhoto = PhotoEmpty.Builder().Id(0).Build();
        byte[]? channelPhotoBytes = _chatRows.GetStoredPhotoBytes(
            ChatPhotos.ReadPhotoId(chat.Value.AsChannel().Get_PhotoView()));
        var botInfo = new Vector();
        var recentRequesters = new VectorOfLong();
        foreach (long requesterId in pendingRequests
                     .OrderByDescending(x => x.Date).ThenBy(x => x.UserId)
                     .Take(3).Select(x => x.UserId))
        {
            recentRequesters.Append(requesterId);
        }
        var fullChannelBuilder = ChannelFull.Builder()
            .Id(channelId.Value)
            .About(about)
            .ParticipantsCount(activeParticipants.Count)
            .ReadInboxMaxId(readInboxMaxId)
            .ReadOutboxMaxId(readOutboxMaxId)
            .UnreadCount(0)
            .ChatPhoto(channelPhotoBytes ?? emptyPhoto.ToReadOnlySpan().ToArray())
            .NotifySettings(notifySettings.ToReadOnlySpan())
            .BotInfo(botInfo)
            .Pts(pts)
            .AvailableReactions(availableReactionsBytes ?? DefaultReactions.AllChatReactionsBytes.ToArray());
        if (callerCanSetStickers)
        {
            fullChannelBuilder = fullChannelBuilder.CanSetStickers(true);
        }
        if (stickerSet is not null)
        {
            fullChannelBuilder = fullChannelBuilder.Stickerset(stickerSet.Value
                .AsStickerSetState().Get_SetView().AsStickerSet().ToReadOnlySpan());
        }
        if (emojiSet is not null)
        {
            fullChannelBuilder = fullChannelBuilder.Emojiset(emojiSet.Value
                .AsStickerSetState().Get_SetView().AsStickerSet().ToReadOnlySpan());
        }
        if (reactionsLimit > 0)
        {
            fullChannelBuilder = fullChannelBuilder.ReactionsLimit(reactionsLimit);
        }
        if (pinnedMsgId > 0)
        {
            fullChannelBuilder = fullChannelBuilder.PinnedMsgId(pinnedMsgId);
        }
        if (exportedInviteBytes != null)
        {
            fullChannelBuilder = fullChannelBuilder.ExportedInvite(exportedInviteBytes);
        }
        if (pendingRequests.Count > 0)
        {
            fullChannelBuilder = fullChannelBuilder
                .RequestsPending(pendingRequests.Count)
                .RecentRequesters(recentRequesters);
        }
        if (migratedFromChatId > 0 && migratedFromMaxId > 0)
        {
            fullChannelBuilder = fullChannelBuilder
                .MigratedFromChatId(migratedFromChatId)
                .MigratedFromMaxId(migratedFromMaxId);
        }
        if (viewForumAsMessages)
        {
            fullChannelBuilder = fullChannelBuilder.ViewForumAsMessages(true);
        }
        if (hiddenPrehistory)
        {
            fullChannelBuilder = fullChannelBuilder.HiddenPrehistory(true);
        }
        if (antispam)
        {
            fullChannelBuilder = fullChannelBuilder.Antispam(true);
        }
        if (participantsHidden)
        {
            fullChannelBuilder = fullChannelBuilder.ParticipantsHidden(true);
        }
        if (slowmodeSeconds > 0)
        {
            fullChannelBuilder = fullChannelBuilder.SlowmodeSeconds(slowmodeSeconds);
        }
        if (slowmodeNextSendDate > 0)
        {
            fullChannelBuilder = fullChannelBuilder
                .SlowmodeNextSendDate(slowmodeNextSendDate);
        }
        if (boostsUnrestrict > 0)
        {
            fullChannelBuilder = fullChannelBuilder.BoostsUnrestrict(boostsUnrestrict);
        }
        if (linkedChatId > 0)
        {
            fullChannelBuilder = fullChannelBuilder.LinkedChatId(linkedChatId);
        }
        if (locationBytes != null)
        {
            fullChannelBuilder = fullChannelBuilder.Location(locationBytes);
        }
        if (mainTabBytes != null)
        {
            fullChannelBuilder = fullChannelBuilder.MainTab(mainTabBytes);
        }
        if (statisticsAvailable && statsDc > 0)
        {
            fullChannelBuilder = fullChannelBuilder.StatsDc(statsDc);
            if (callerIsAdmin)
            {
                fullChannelBuilder = fullChannelBuilder.CanViewStats(true);
            }
        }
        if (isMegagroup && callerCanChangeInfo)
        {
            fullChannelBuilder = fullChannelBuilder.CanSetLocation(true);
        }
        if (callLink.Call is { } linkedCall)
        {
            fullChannelBuilder = fullChannelBuilder.Call(linkedCall.AsSpan());
        }
        if (callerIsActiveMember && callLink.DefaultJoinAs is { } defaultJoinAs)
        {
            fullChannelBuilder = fullChannelBuilder
                .GroupcallDefaultJoinAs(defaultJoinAs.AsSpan());
        }

        if (!string.IsNullOrEmpty(channelSettings.ThemeEmoticon))
        {
            fullChannelBuilder = fullChannelBuilder.ThemeEmoticon(
                System.Text.Encoding.UTF8.GetBytes(channelSettings.ThemeEmoticon));
        }
        if (channelSettings.TtlPeriod > 0)
        {
            fullChannelBuilder = fullChannelBuilder.TtlPeriod(channelSettings.TtlPeriod);
        }
        if (wallpaper is { } storedWallpaper)
        {
            var row = storedWallpaper.AsPeerWallpaper();
            if (row.Flags[2])
            {
                fullChannelBuilder = fullChannelBuilder.Wallpaper(row.Wallpaper);
            }
        }
        using TLPeer defaultSendAsPeer = defaultSendAs is { } sendAs
            ? PeerResolver.BuildPeer(sendAs.Type, sendAs.Id)
            : default;
        if (defaultSendAs != null)
        {
            fullChannelBuilder = fullChannelBuilder
                .DefaultSendAs(defaultSendAsPeer.AsSpan());
        }

        byte[] channelRowBytes = ChannelRows.ForViewer(chat.Value.AsSpan().ToArray(),
            callerIsActiveMember, callerIsCreator);

        using Ferrite.TL.baseLayer.TLChatFull fullChannel = fullChannelBuilder.Build();
        var chatVector = new Vector();
        chatVector.AppendTLObject(channelRowBytes);
        if (migratedFromChatBytes != null)
        {
            chatVector.AppendTLObject(migratedFromChatBytes);
        }
        var userVector = new Vector();
        AppendUsers(currentUserId, ref userVector, activeParticipants
            .Select(p => p.AsChatParticipantInfo().UserId)
            .Concat(pendingRequests.Select(x => x.UserId)));

        return Ferrite.TL.baseLayer.messages.MessagesChatFull.Builder()
            .FullChat(fullChannel.AsSpan())
            .Chats(chatVector)
            .Users(userVector)
            .Build();
    }
}
