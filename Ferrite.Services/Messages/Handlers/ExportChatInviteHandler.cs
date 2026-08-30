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

public sealed class ExportChatInviteHandler : MessagesHandlerBase
{
    private readonly IChatInvitesRepository _chatInvitesRepository;

    public ExportChatInviteHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatInvitesRepository chatInvitesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
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

    [TLFunction(Constructors.baseLayer_ExportChatInvite)]
    public async Task<Ferrite.TL.baseLayer.TLExportedChatInvite> Handle(
            long authKeyId, TLBytes q)
        {
            var request = (ExportChatInvite)q;
            (bool isChannel, long chatId) = PeerResolver.ResolveInviteChatPeer(request.Get_PeerView());
            bool legacyRevokePermanent = request.LegacyRevokePermanent;
            bool requestNeeded = request.RequestNeeded;
            int expireDate = request.ExpireDate;
            int usageLimit = request.UsageLimit;
            byte[] title = request.Title.ToArray();

            var (context, error) = await PrepareInviteAdmin(authKeyId, isChannel, chatId);
            if (error != null)
            {
                return ErrorExportedInvite(error);
            }

            int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
            string hash = ChatInvites.GenerateHash();
            TLChatInviteInfo newInvite;
            if (legacyRevokePermanent)
            {
                var invites = await _invites.GetStoredInvitesAsync(chatId);
                foreach (var invite in invites)
                {
                    if (invite.Permanent && !invite.Revoked)
                    {
                        _invites.PutStoredInvite(invite, revoked: true, invite.RequestNeeded,
                            invite.ExpireDate, invite.UsageLimit, invite.Usage, invite.Title);
                    }
                }
                newInvite = ChatInvites.BuildInviteInfo(chatId, hash, context!.CurrentUserId,
                    date, permanent: true, revoked: false, requestNeeded: false,
                    expireDate: 0, usageLimit: 0, usage: 0, title: null);
            }
            else
            {
                newInvite = ChatInvites.BuildInviteInfo(chatId, hash, context!.CurrentUserId,
                    date, permanent: false, revoked: false, requestNeeded, expireDate,
                    usageLimit, usage: 0, title.Length > 0 ? title : null);
            }
            _chatInvitesRepository.PutInvite(newInvite);
            await _unitOfWork.SaveAsync();

            byte[] inviteBytes = newInvite.AsChatInviteInfo().Invite.ToArray();
            newInvite.Dispose();
            _log.Debug($"🔗 ExportChatInvite user:{context.CurrentUserId} chat:{chatId} " +
                       $"channel:{isChannel} permanent:{legacyRevokePermanent} hash:{hash}");
            return new Ferrite.TL.baseLayer.TLExportedChatInvite(inviteBytes, 0, inviteBytes.Length);
        }
}
