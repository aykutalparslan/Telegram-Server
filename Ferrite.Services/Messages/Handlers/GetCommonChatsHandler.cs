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

public sealed class GetCommonChatsHandler : MessagesHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    public GetCommonChatsHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

    }

    [TLFunction(Constructors.baseLayer_GetCommonChats)]
    public async Task<TLChats> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return (TLChats)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
            }

            long currentUserId = auth.Value.AsAuthInfo().UserId;
            var request = (GetCommonChats)q;
            long? targetUserId = ResolveInputUserId(request.Get_UserIdView(), currentUserId);
            long maxId = request.MaxId;
            int limit = request.Limit;
            if (targetUserId == null)
            {
                return (TLChats)RpcErrorGenerator.GenerateError(400, "USER_ID_INVALID"u8);
            }

            var currentChats = await _chatParticipantsRepository
                .GetParticipantsByUserAsync(currentUserId);
            var targetChats = await _chatParticipantsRepository
                .GetParticipantsByUserAsync(targetUserId.Value);
            var targetChatIds = targetChats
                .Where(IsActiveParticipant)
                .Select(p => p.AsChatParticipantInfo().ChatId)
                .ToHashSet();
            var commonChatIds = currentChats
                .Where(IsActiveParticipant)
                .Select(p => p.AsChatParticipantInfo().ChatId)
                .Where(id => targetChatIds.Contains(id))
                .Where(id => maxId <= 0 || id < maxId)
                .OrderByDescending(id => id)
                .Take(Math.Max(0, limit));

            var chatBytes = new List<byte[]>();
            foreach (long chatId in commonChatIds)
            {
                using var chat = await _chatRepository.GetChatAsync(chatId);
                if (chat != null)
                {
                    chatBytes.Add(chat.Value.AsSpan().ToArray());
                }
            }

            var chatVector = new Vector();
            foreach (byte[] bytes in chatBytes)
            {
                chatVector.AppendTLObject(bytes);
            }

            return Chats.Builder()
                .ChatsProperty(chatVector)
                .Build();
        }
}
