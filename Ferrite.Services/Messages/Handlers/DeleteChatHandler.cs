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

public sealed class DeleteChatHandler : MessagesHandlerBase
{
    private readonly IChatInvitesRepository _chatInvitesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    public DeleteChatHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatInvitesRepository chatInvitesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
        _chatInvitesRepository = chatInvitesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

    }

    [TLFunction(Constructors.baseLayer_DeleteChat)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
        {
            long chatId = ((DeleteChat)q).ChatId;
            var (context, error) = await PrepareBasicChatMutation(authKeyId, chatId,
                requireAdmin: true, requireCreator: true);
            if (error != null)
            {
                return ErrorBool(error);
            }

            try
            {
                var participantIds = new List<long>(context.ActiveParticipants.Count);
                foreach (var participantInfo in context.ActiveParticipants)
                {
                    participantIds.Add(participantInfo.AsChatParticipantInfo().UserId);
                }

                // Wipe every member's copy of the conversation (count-based delete
                // updates), then deactivate the compact chat row and drop the
                // participants and full-info rows.
                foreach (long participantId in participantIds)
                {
                    IUpdatesContext participantCtx = participantId == context.CurrentUserId
                        ? _updatesContextFactory.GetUpdatesContext(authKeyId, participantId)
                        : _updatesContextFactory.GetUpdatesContext(null, participantId);
                    await DeleteConversation(participantId, TLPeer.PeerType.PeerChat, chatId,
                        maxId: 0, minDate: null, maxDate: null, participantCtx);
                }

                MarkStoredChatDeactivated(context.ChatBytes);
                _chatParticipantsRepository.DeleteParticipants(chatId);
                _chatInvitesRepository.DeleteInvites(chatId);
                _chatInvitesRepository.DeleteImporters(chatId);
                _chatRepository.DeleteFullInfo(chatId);
                await _unitOfWork.SaveAsync();

                foreach (long participantId in participantIds)
                {
                    TLUpdate update = UpdateChat.Builder()
                        .ChatId(chatId)
                        .Build();
                    await _updates.EnqueueUpdate(participantId, update);
                }

                _log.Debug($"👥 DeleteChat user:{context.CurrentUserId} chat:{chatId} " +
                           $"participants:{participantIds.Count}");
                return BoolTrue.Builder().Build();
            }
            finally
            {
                DisposeParticipants(context.ActiveParticipants);
            }
        }
}
