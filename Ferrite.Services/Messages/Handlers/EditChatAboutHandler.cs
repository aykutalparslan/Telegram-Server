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

public sealed class EditChatAboutHandler : MessagesHandlerBase
{
    public EditChatAboutHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
    }

    [TLFunction(Constructors.baseLayer_EditChatAbout)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
        {
            var request = (EditChatAbout)q;
            var peer = request.Get_PeerView();
            byte[] about = request.About.ToArray();
            long chatId;
            if (peer.Is(out InputPeerChat chatPeer))
            {
                chatId = chatPeer.ChatId;
            }
            else if (peer.Is(out InputPeerChannel channelPeer))
            {
                return await EditChannelAbout(authKeyId, channelPeer.ChannelId, about);
            }
            else
            {
                return ErrorBool("PEER_ID_INVALID");
            }

            var (context, error) = await PrepareBasicChatMutation(authKeyId, chatId, requireAdmin: true);
            if (error != null)
            {
                return ErrorBool(error);
            }

            try
            {
                await PutChatAbout(chatId, about);
                await _unitOfWork.SaveAsync();
                await _fanout.PushUpdateChatAsync(chatId, context.ActiveParticipants
                    .Select(p => p.AsChatParticipantInfo().UserId));
                _log.Debug($"👥 EditChatAbout user:{context.CurrentUserId} chat:{chatId}");
                return BoolTrue.Builder().Build();
            }
            finally
            {
                DisposeParticipants(context.ActiveParticipants);
            }
        }
}
