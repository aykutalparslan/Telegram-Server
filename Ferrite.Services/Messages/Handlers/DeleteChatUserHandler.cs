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

public sealed class DeleteChatUserHandler : MessagesHandlerBase
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    public DeleteChatUserHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
        _chatParticipantsRepository = chatParticipantsRepository;

    }

    [TLFunction(Constructors.baseLayer_DeleteChatUser)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
        {
            var request = (DeleteChatUser)q;
            long chatId = request.ChatId;
            bool revokeHistory = request.RevokeHistory;

            var (context, error) = await PrepareBasicChatMutation(authKeyId, chatId, requireAdmin: false);
            if (error != null)
            {
                return ErrorUpdates(error);
            }

            try
            {
                long? targetUserId = ResolveInputUserId(((DeleteChatUser)q).Get_UserIdView(),
                    context.CurrentUserId);
                if (targetUserId is not > 0)
                {
                    return ErrorUpdates("USER_ID_INVALID");
                }

                long targetId = targetUserId.Value;
                bool targetFound = false;
                int targetRole = 0;
                long targetInviterId = 0;
                int currentRole = 0;
                foreach (var participantInfo in context.ActiveParticipants)
                {
                    var info = participantInfo.AsChatParticipantInfo();
                    if (info.UserId == targetId)
                    {
                        targetFound = true;
                        targetRole = info.Role;
                        targetInviterId = info.InviterId;
                    }
                    if (info.UserId == context.CurrentUserId)
                    {
                        currentRole = info.Role;
                    }
                }

                if (!targetFound)
                {
                    return ErrorUpdates("USER_NOT_PARTICIPANT");
                }

                if (targetId != context.CurrentUserId)
                {
                    bool allowed = currentRole == (int)ChatParticipantRole.Creator ||
                        (targetRole != (int)ChatParticipantRole.Creator &&
                         targetRole != (int)ChatParticipantRole.Admin &&
                         (currentRole == (int)ChatParticipantRole.Admin ||
                          targetInviterId == context.CurrentUserId));
                    if (!allowed)
                    {
                        return ErrorUpdates("CHAT_ADMIN_REQUIRED");
                    }
                }

                foreach (var participantInfo in context.ActiveParticipants)
                {
                    var info = participantInfo.AsChatParticipantInfo();
                    if (info.UserId != targetId)
                    {
                        continue;
                    }
                    using TLChatParticipantInfo leftParticipant = info.Clone()
                        .Role((int)ChatParticipantRole.Left)
                        .Build();
                    _chatParticipantsRepository.PutParticipant(leftParticipant);
                    break;
                }

                byte[] updatedChatBytes = _chatRows.UpdateStoredChatMembership(context.ChatBytes, -1);
                int newVersion = ReadChatVersion(updatedChatBytes);

                if (revokeHistory)
                {
                    IUpdatesContext targetCtx = targetId == context.CurrentUserId
                        ? _updatesContextFactory.GetUpdatesContext(authKeyId, targetId)
                        : _updatesContextFactory.GetUpdatesContext(null, targetId);
                    await DeleteConversation(targetId, TLPeer.PeerType.PeerChat, chatId,
                        maxId: 0, minDate: null, maxDate: null, targetCtx);
                }

                var remainingParticipants = new List<TLChatParticipantInfo>();
                foreach (var participantInfo in context.ActiveParticipants)
                {
                    if (participantInfo.AsChatParticipantInfo().UserId != targetId)
                    {
                        remainingParticipants.Add(participantInfo);
                    }
                }

                byte[] actionBytes;
                using (TLMessageAction action = MessageActionChatDeleteUser.Builder()
                           .UserId(targetId)
                           .Build())
                {
                    actionBytes = action.AsSpan().ToArray();
                }

                byte[] participantsUpdateBytes =
                    BuildChatParticipantsUpdateBytes(chatId, remainingParticipants, newVersion);
                _log.Debug($"👥 DeleteChatUser user:{context.CurrentUserId} chat:{chatId} " +
                           $"target:{targetId} revoke:{revokeHistory}");
                return await EmitBasicGroupServiceUpdates(authKeyId, context.CurrentUserId,
                    chatId, context.ActiveParticipants, actionBytes, updatedChatBytes,
                    participantsUpdateBytes);
            }
            finally
            {
                DisposeParticipants(context.ActiveParticipants);
            }
        }
}
