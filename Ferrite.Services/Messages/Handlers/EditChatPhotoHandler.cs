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

public sealed class EditChatPhotoHandler : MessagesHandlerBase
{
    private readonly IPhotoRepository _photoRepository;

    public EditChatPhotoHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IPhotoRepository photoRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
        _photoRepository = photoRepository;

    }

    [TLFunction(Constructors.baseLayer_EditChatPhoto)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
        {
            var request = (EditChatPhoto)q;
            long chatId = request.ChatId;
            byte[] photoBytes = request.Photo.ToArray();

            var (context, error) = await PrepareBasicChatMutation(authKeyId, chatId, requireAdmin: true);
            if (error != null)
            {
                return ErrorUpdates(error);
            }

            try
            {
                var resolution = await ChatPhotos.ResolveAsync(photoBytes, _upload, _photos, _photoRepository);
                if (resolution.Error != null)
                {
                    return ErrorUpdates(resolution.Error.Value.Message);
                }

                byte[] updatedChatBytes;
                byte[] actionBytes;
                if (resolution.IsDelete)
                {
                    updatedChatBytes = _chatRows.UpdateStoredChatPhotoEmpty(context.ChatBytes);
                    using TLMessageAction action = MessageActionChatDeletePhoto.Builder().Build();
                    actionBytes = action.AsSpan().ToArray();
                    _log.Debug($"👥 EditChatPhoto(empty) user:{context.CurrentUserId} chat:{chatId}");
                }
                else
                {
                    updatedChatBytes = _chatRows.UpdateStoredChatPhoto(context.ChatBytes, resolution.PhotoId);
                    using TLMessageAction action = MessageActionChatEditPhoto.Builder()
                        .Photo(resolution.PhotoBytes)
                        .Build();
                    actionBytes = action.AsSpan().ToArray();
                    _log.Debug($"👥 EditChatPhoto user:{context.CurrentUserId} chat:{chatId} " +
                               $"photo:{resolution.PhotoId}");
                }

                return await EmitBasicGroupServiceUpdates(authKeyId, context.CurrentUserId,
                    chatId, context.ActiveParticipants, actionBytes, updatedChatBytes);
            }
            finally
            {
                DisposeParticipants(context.ActiveParticipants);
            }
        }
}
