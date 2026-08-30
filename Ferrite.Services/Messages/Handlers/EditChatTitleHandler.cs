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

public sealed class EditChatTitleHandler : MessagesHandlerBase
{
    public EditChatTitleHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
    }

    [TLFunction(Constructors.baseLayer_EditChatTitle)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
        {
            var request = (EditChatTitle)q;
            long chatId = request.ChatId;
            byte[] title = request.Title.ToArray();

            var (context, error) = await PrepareBasicChatMutation(authKeyId, chatId, requireAdmin: true);
            if (error != null)
            {
                return ErrorUpdates(error);
            }

            try
            {
                byte[] updatedChatBytes = _chatRows.UpdateStoredChatTitle(context.ChatBytes, title);
                byte[] actionBytes;
                using (TLMessageAction action = MessageActionChatEditTitle.Builder()
                           .Title(title)
                           .Build())
                {
                    actionBytes = action.AsSpan().ToArray();
                }

                _log.Debug($"👥 EditChatTitle user:{context.CurrentUserId} chat:{chatId}");
                return await EmitBasicGroupServiceUpdates(authKeyId, context.CurrentUserId,
                    chatId, context.ActiveParticipants, actionBytes, updatedChatBytes);
            }
            finally
            {
                DisposeParticipants(context.ActiveParticipants);
            }
        }
}
