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

public sealed class GetExportedChatInviteHandler : MessagesHandlerBase
{
    public GetExportedChatInviteHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
    }

    [TLFunction(Constructors.baseLayer_GetExportedChatInvite)]
    public async Task<Ferrite.TL.baseLayer.messages.TLExportedChatInvite> Handle(
            long authKeyId, TLBytes q)
        {
            var request = (GetExportedChatInvite)q;
            (bool isChannel, long chatId) = PeerResolver.ResolveInviteChatPeer(request.Get_PeerView());
            string hash = ChatInvites.HashFromLink(Encoding.UTF8.GetString(request.Link));

            var (context, error) = await PrepareInviteAdmin(authKeyId, isChannel, chatId);
            if (error != null)
            {
                return ErrorMessagesExportedInvite(error);
            }

            var invite = await _invites.GetStoredInviteAsync(chatId, hash);
            if (invite == null)
            {
                return ErrorMessagesExportedInvite("INVITE_HASH_INVALID");
            }

            var userVector = new Vector();
            AppendUsers(ref userVector, new[] { invite.AdminId });
            return Ferrite.TL.baseLayer.messages.ExportedChatInvite.Builder()
                .Invite(invite.InviteBytes)
                .Users(userVector)
                .Build();
        }
}
