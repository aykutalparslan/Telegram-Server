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

public sealed class DeleteExportedChatInviteHandler : MessagesHandlerBase
{
    private readonly IChatInvitesRepository _chatInvitesRepository;

    public DeleteExportedChatInviteHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatInvitesRepository chatInvitesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
        _chatInvitesRepository = chatInvitesRepository;

    }

    [TLFunction(Constructors.baseLayer_DeleteExportedChatInvite)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
        {
            var request = (DeleteExportedChatInvite)q;
            (bool isChannel, long chatId) = PeerResolver.ResolveInviteChatPeer(request.Get_PeerView());
            string hash = ChatInvites.HashFromLink(Encoding.UTF8.GetString(request.Link));

            var (context, error) = await PrepareInviteAdmin(authKeyId, isChannel, chatId);
            if (error != null)
            {
                return ErrorBool(error);
            }

            var invite = await _invites.GetStoredInviteAsync(chatId, hash);
            if (invite == null)
            {
                return ErrorBool("INVITE_HASH_INVALID");
            }
            if (!context!.IsCreator && invite.AdminId != context.CurrentUserId)
            {
                return ErrorBool("CHAT_ADMIN_REQUIRED");
            }

            _chatInvitesRepository.DeleteInvite(chatId, hash);
            await _unitOfWork.SaveAsync();
            _log.Debug($"🔗 DeleteExportedChatInvite user:{context.CurrentUserId} chat:{chatId} " +
                       $"hash:{hash}");
            return new BoolTrue();
        }
}
