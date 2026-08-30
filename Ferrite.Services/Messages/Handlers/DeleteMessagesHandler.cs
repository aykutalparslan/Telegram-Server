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

public sealed class DeleteMessagesHandler : MessagesHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;

    public DeleteMessagesHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
        _authorizationRepository = authorizationRepository;

    }

    [TLFunction(Constructors.baseLayer_MessagesDeleteMessages)]
    public async Task<TLAffectedMessages> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            long userId = auth.Value.AsAuthInfo().UserId;
            var userCtx = _updatesContextFactory.GetUpdatesContext(authKeyId, userId);

            var idVector = ((MessagesDeleteMessages)q).Id;
            int count = idVector.Count;
            var deletedIds = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                deletedIds.Add(idVector[i]);
            }

            _messages.DeleteMessages(userId, deletedIds);
            await _unitOfWork.SaveAsync();

            int pts = await _fanout.AdvanceAndEnqueueDeleteMessagesAsync(userId,
                deletedIds, userCtx);
            _log.Debug($"🗑 DeleteMessages user:{userId} count:{deletedIds.Count} pts:{pts}");
            return AffectedMessages.Builder().Pts(pts).PtsCount(deletedIds.Count).Build();
        }
}
