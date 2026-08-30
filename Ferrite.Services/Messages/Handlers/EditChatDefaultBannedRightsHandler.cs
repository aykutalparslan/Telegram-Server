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

public sealed class EditChatDefaultBannedRightsHandler : MessagesHandlerBase
{
    public EditChatDefaultBannedRightsHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
    }

    [TLFunction(Constructors.baseLayer_EditChatDefaultBannedRights)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
        {
            var peer = ((EditChatDefaultBannedRights)q).Get_PeerView();
            bool toChat = peer.Is(out InputPeerChat chatPeer);
            long chatId = toChat ? chatPeer.ChatId : 0;
            long channelId = PeerResolver.ResolveInputPeerChannelId(peer);
            byte[] rightsBytes = ((EditChatDefaultBannedRights)q).BannedRights.ToArray();

            if (ChatRights.BansViewMessages(rightsBytes))
            {
                return ErrorUpdates("BANNED_RIGHTS_INVALID");
            }

            if (toChat)
            {
                return await EditChatDefaultBannedRightsForChat(authKeyId, chatId, rightsBytes);
            }
            if (channelId > 0)
            {
                return await EditChatDefaultBannedRightsForChannel(authKeyId, channelId, rightsBytes);
            }

            return ErrorUpdates("PEER_ID_INVALID");
        }
}
