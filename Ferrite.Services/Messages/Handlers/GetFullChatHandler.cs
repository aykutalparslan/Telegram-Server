// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetFullChatHandler : MessagesHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatInvitesRepository _chatInvitesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    public GetFullChatHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatInvitesRepository chatInvitesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs, GroupCallChatLink groupCallLink,
        ChatSettingsStore settings)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
        _authorizationRepository = authorizationRepository;
        _chatInvitesRepository = chatInvitesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _groupCallLink = groupCallLink;
        _settings = settings;
    }

    private readonly GroupCallChatLink _groupCallLink;
    private readonly ChatSettingsStore _settings;

    [TLFunction(Constructors.baseLayer_GetFullChat)]
    public async Task<Ferrite.TL.baseLayer.messages.TLChatFull> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return (Ferrite.TL.baseLayer.messages.TLChatFull)RpcErrorGenerator
                    .GenerateError(400, "AUTH_KEY_INVALID"u8);
            }

            long currentUserId = auth.Value.AsAuthInfo().UserId;
            long chatId = ((GetFullChat)q).ChatId;
            var participant = await _chatParticipantsRepository
                .GetParticipantAsync(chatId, currentUserId);
            if (participant == null || !IsActiveParticipant(participant.Value))
            {
                participant?.Dispose();
                return (Ferrite.TL.baseLayer.messages.TLChatFull)RpcErrorGenerator
                    .GenerateError(400, "USER_NOT_PARTICIPANT"u8);
            }
            participant.Value.Dispose();

            using var chat = await _chatRepository.GetChatAsync(chatId);
            if (chat == null)
            {
                return (Ferrite.TL.baseLayer.messages.TLChatFull)RpcErrorGenerator
                    .GenerateError(400, "CHAT_ID_INVALID"u8);
            }

            var participantInfos = await _chatParticipantsRepository.GetParticipantsAsync(chatId);
            var activeParticipants = participantInfos.Where(IsActiveParticipant).ToList();
            int chatVersion = chat.Value.AsChat().Version;
            _log.Debug($"👥 GetFullChat user:{currentUserId} chat:{chatId} version:{chatVersion} " +
                       $"active:{activeParticipants.Count}/{participantInfos.Count}");
            using TLChatParticipants participants =
                BuildChatParticipants(chatId, activeParticipants, chatVersion);
            byte[] about = Array.Empty<byte>();
            int pinnedMsgId = 0;
            byte[]? availableReactionsBytes = null;
            int reactionsLimit = 0;
            using var storedFullInfo = await _chatRepository.GetFullInfoAsync(chatId);
            if (storedFullInfo != null)
            {
                var fullInfo = storedFullInfo.Value.AsChatFullInfo();
                about = fullInfo.About.ToArray();
                pinnedMsgId = fullInfo.PinnedMsgId;
                if (fullInfo.Flags[2])
                {
                    availableReactionsBytes = fullInfo.AvailableReactions.ToArray();
                }
                reactionsLimit = fullInfo.ReactionsLimit;
            }

            // The permanent invite link surfaces only to callers who can manage invites
            // (the basic-group creator or admins).
            bool callerManagesInvites = false;
            foreach (var participantInfo in activeParticipants)
            {
                var info = participantInfo.AsChatParticipantInfo();
                if (info.UserId != currentUserId)
                {
                    continue;
                }
                callerManagesInvites = info.Role is (int)ChatParticipantRole.Creator
                    or (int)ChatParticipantRole.Admin;
                break;
            }
            byte[]? exportedInviteBytes = callerManagesInvites
                ? await ChatInvites.GetPermanentInviteBytesAsync(
                    _chatInvitesRepository, chatId)
                : null;
            List<PendingInviteImporter> pendingRequests = callerManagesInvites
                ? await ChatInvites.GetPendingImportersAsync(
                    _chatInvitesRepository, chatId)
                : new List<PendingInviteImporter>();

            // Resolved before the builder exists: ChatFull.Builder() is a ref struct
            // that cannot span an await, and the join-as is this viewer's own.
            using GroupCallFullLink callLink = await _groupCallLink.ResolveFullLinkAsync(
                GroupCallPeerType.Chat, chatId, currentUserId);
            ChatSettingsSnapshot chatSettings = await _settings.GetAsync(
                ChatSettingsScope.ForChat(chatId));

            using var notifySettings = PeerNotifySettings.Builder().Build();
            var recentRequesters = new VectorOfLong();
            foreach (long requesterId in pendingRequests
                         .OrderByDescending(x => x.Date).ThenBy(x => x.UserId)
                         .Take(3).Select(x => x.UserId))
            {
                recentRequesters.Append(requesterId);
            }
            var fullChatBuilder = ChatFull.Builder()
                .Id(chatId)
                .About(about)
                .Participants(participants.AsSpan())
                .NotifySettings(notifySettings.ToReadOnlySpan())
                .AvailableReactions(availableReactionsBytes ?? DefaultReactions.AllChatReactionsBytes.ToArray());
            if (reactionsLimit > 0)
            {
                fullChatBuilder = fullChatBuilder.ReactionsLimit(reactionsLimit);
            }
            if (pinnedMsgId > 0)
            {
                fullChatBuilder = fullChatBuilder.PinnedMsgId(pinnedMsgId);
            }
            if (exportedInviteBytes != null)
            {
                fullChatBuilder = fullChatBuilder.ExportedInvite(exportedInviteBytes);
            }
            if (pendingRequests.Count > 0)
            {
                fullChatBuilder = fullChatBuilder
                    .RequestsPending(pendingRequests.Count)
                    .RecentRequesters(recentRequesters);
            }
            byte[]? chatPhotoBytes = _chatRows.GetStoredPhotoBytes(
                ChatPhotos.ReadPhotoId(chat.Value.AsChat().Get_PhotoView()));
            if (chatPhotoBytes != null)
            {
                fullChatBuilder = fullChatBuilder.ChatPhoto(chatPhotoBytes);
            }
            if (callLink.Call is { } linkedCall)
            {
                fullChatBuilder = fullChatBuilder.Call(linkedCall.AsSpan());
            }
            if (callLink.DefaultJoinAs is { } defaultJoinAs)
            {
                fullChatBuilder = fullChatBuilder.GroupcallDefaultJoinAs(defaultJoinAs.AsSpan());
            }
            if (!string.IsNullOrEmpty(chatSettings.ThemeEmoticon))
            {
                fullChatBuilder = fullChatBuilder.ThemeEmoticon(
                    System.Text.Encoding.UTF8.GetBytes(chatSettings.ThemeEmoticon));
            }
            if (chatSettings.TtlPeriod > 0)
            {
                fullChatBuilder = fullChatBuilder.TtlPeriod(chatSettings.TtlPeriod);
            }

            using Ferrite.TL.baseLayer.TLChatFull fullChat = fullChatBuilder.Build();
            var chatVector = new Vector();
            chatVector.AppendTLObject(chat.Value.AsSpan());
            var userVector = new Vector();
            AppendUsers(ref userVector, activeParticipants
                .Select(p => p.AsChatParticipantInfo().UserId)
                .Concat(pendingRequests.Select(x => x.UserId)));

            return MessagesChatFull.Builder()
                .FullChat(fullChat.AsSpan())
                .Chats(chatVector)
                .Users(userVector)
                .Build();
        }
}
