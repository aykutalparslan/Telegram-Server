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

public sealed class GetAdminsWithInvitesHandler : MessagesHandlerBase
{
    public GetAdminsWithInvitesHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
    }

    [TLFunction(Constructors.baseLayer_GetAdminsWithInvites)]
    public async Task<Ferrite.TL.baseLayer.messages.TLChatAdminsWithInvites> Handle(
            long authKeyId, TLBytes q)
        {
            var request = (GetAdminsWithInvites)q;
            (bool isChannel, long chatId) = PeerResolver.ResolveInviteChatPeer(request.Get_PeerView());

            var (context, error) = await PrepareInviteAdmin(authKeyId, isChannel, chatId);
            if (error != null)
            {
                return ErrorChatAdminsWithInvites(error);
            }
            if (!context!.IsCreator)
            {
                // Per-admin invite stats are owner-only.
                return ErrorChatAdminsWithInvites("CHAT_ADMIN_REQUIRED");
            }

            var invites = await _invites.GetStoredInvitesAsync(chatId);
            var byAdmin = new Dictionary<long, (int Invites, int Revoked)>();
            foreach (var invite in invites)
            {
                var counts = byAdmin.TryGetValue(invite.AdminId, out var existing)
                    ? existing
                    : (Invites: 0, Revoked: 0);
                byAdmin[invite.AdminId] = invite.Revoked
                    ? (counts.Invites, counts.Revoked + 1)
                    : (counts.Invites + 1, counts.Revoked);
            }

            var adminsVector = new Vector();
            foreach (var (adminId, counts) in byAdmin.OrderBy(kvp => kvp.Key))
            {
                using var admin = ChatAdminWithInvites.Builder()
                    .AdminId(adminId)
                    .InvitesCount(counts.Invites)
                    .RevokedInvitesCount(counts.Revoked)
                    .Build();
                adminsVector.AppendTLObject(admin.ToReadOnlySpan());
            }
            var userVector = new Vector();
            AppendUsers(ref userVector, byAdmin.Keys);

            return ChatAdminsWithInvites.Builder()
                .Admins(adminsVector)
                .Users(userVector)
                .Build();
        }
}
