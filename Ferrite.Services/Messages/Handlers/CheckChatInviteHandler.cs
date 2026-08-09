// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class CheckChatInviteHandler : MessagesHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    public CheckChatInviteHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

    }

    [TLFunction(Constructors.baseLayer_CheckChatInvite)]
    public async Task<TLChatInvite> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return ErrorChatInvite("AUTH_KEY_INVALID");
            }

            long currentUserId = auth.Value.AsAuthInfo().UserId;
            string hash = Encoding.UTF8.GetString(((CheckChatInvite)q).Hash);
            var invite = _invites.GetStoredInviteByHash(hash);
            if (invite == null)
            {
                return ErrorChatInvite("INVITE_HASH_INVALID");
            }
            int now = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
            if (invite.Revoked || (invite.ExpireDate > 0 && invite.ExpireDate <= now))
            {
                return ErrorChatInvite("INVITE_HASH_EXPIRED");
            }

            byte[] chatBytes;
            byte[] titleBytes;
            int participantsCount;
            bool chatIsChannel;
            bool broadcast = false;
            bool megagroup = false;
            bool hasUsername = false;
            {
                using var chat = await _chatRepository.GetChatAsync(invite.ChatId);
                if (chat == null)
                {
                    return ErrorChatInvite("INVITE_HASH_EXPIRED");
                }
                chatIsChannel = chat.Value.Type == TLChat.ChatType.Channel;
                if (chatIsChannel)
                {
                    var channel = chat.Value.AsChannel();
                    titleBytes = channel.Title.ToArray();
                    participantsCount = channel.ParticipantsCount;
                    broadcast = channel.Broadcast;
                    megagroup = channel.Megagroup;
                    hasUsername = channel.Username.Length > 0;
                }
                else if (chat.Value.Type == TLChat.ChatType.Chat &&
                         !chat.Value.AsChat().Deactivated)
                {
                    var basicChat = chat.Value.AsChat();
                    titleBytes = basicChat.Title.ToArray();
                    participantsCount = basicChat.ParticipantsCount;
                }
                else
                {
                    return ErrorChatInvite("INVITE_HASH_EXPIRED");
                }
                chatBytes = chat.Value.AsSpan().ToArray();
            }

            var participant = await _chatParticipantsRepository
                .GetParticipantAsync(invite.ChatId, currentUserId);
            bool alreadyMember = participant != null && IsActiveParticipant(participant.Value);
            participant?.Dispose();
            _log.Debug($"🔗 CheckChatInvite user:{currentUserId} chat:{invite.ChatId} " +
                       $"hash:{hash} member:{alreadyMember}");
            if (alreadyMember)
            {
                return ChatInviteAlready.Builder()
                    .Chat(chatBytes)
                    .Build();
            }

            byte[] about = Array.Empty<byte>();
            using (var fullInfo = await _chatRepository.GetFullInfoAsync(invite.ChatId))
            {
                if (fullInfo != null)
                {
                    about = fullInfo.Value.AsChatFullInfo().About.ToArray();
                }
            }

            using var photo = PhotoEmpty.Builder().Id(0).Build();
            var previewBuilder = ChatInvite.Builder()
                .Title(titleBytes)
                .Photo(photo.ToReadOnlySpan())
                .ParticipantsCount(participantsCount)
                .Color(0);
            if (chatIsChannel)
            {
                previewBuilder = previewBuilder.Channel(true);
                if (broadcast)
                {
                    previewBuilder = previewBuilder.Broadcast(true);
                }
                if (megagroup)
                {
                    previewBuilder = previewBuilder.Megagroup(true);
                }
                if (hasUsername)
                {
                    previewBuilder = previewBuilder.PublicProperty(true);
                }
            }
            if (invite.RequestNeeded)
            {
                previewBuilder = previewBuilder.RequestNeeded(true);
            }
            if (about.Length > 0)
            {
                previewBuilder = previewBuilder.About(about);
            }

            return previewBuilder.Build();
        }
}
