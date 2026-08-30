// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class DeleteRevokedExportedChatInvitesHandler : MessagesHandlerBase
{
    private readonly IChatInvitesRepository _chatInvitesRepository;

    public DeleteRevokedExportedChatInvitesHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatInvitesRepository chatInvitesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
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

    [TLFunction(Constructors.baseLayer_DeleteRevokedExportedChatInvites)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
        {
            var request = (DeleteRevokedExportedChatInvites)q;
            (bool isChannel, long chatId) = PeerResolver.ResolveInviteChatPeer(request.Get_PeerView());
            (bool adminIsSelf, long adminUserId) = PeerResolver.ReadInputUser(request.Get_AdminIdView());

            var (context, error) = await PrepareInviteAdmin(authKeyId, isChannel, chatId);
            if (error != null)
            {
                return ErrorBool(error);
            }

            long adminFilter = adminIsSelf || adminUserId <= 0 ? context!.CurrentUserId : adminUserId;
            if (adminFilter != context!.CurrentUserId && !context.IsCreator)
            {
                return ErrorBool("CHAT_ADMIN_REQUIRED");
            }

            var invites = await _invites.GetStoredInvitesAsync(chatId);
            int deleted = 0;
            foreach (var invite in invites)
            {
                if (invite.Revoked && invite.AdminId == adminFilter)
                {
                    _chatInvitesRepository.DeleteInvite(chatId, invite.Hash);
                    deleted++;
                }
            }
            await _unitOfWork.SaveAsync();
            _log.Debug($"🔗 DeleteRevokedExportedChatInvites user:{context.CurrentUserId} " +
                       $"chat:{chatId} admin:{adminFilter} deleted:{deleted}");
            return new BoolTrue();
        }
}
