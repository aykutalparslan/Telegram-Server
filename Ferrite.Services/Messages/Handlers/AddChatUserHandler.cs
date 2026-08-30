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

public sealed class AddChatUserHandler : MessagesHandlerBase
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    public AddChatUserHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
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

    [TLFunction(Constructors.layer133_MessagesAddChatUser)]
    public async Task<TLInvitedUsers> HandleLayer133(long authKeyId, TLBytes q)
    {
        using var current = ToCurrentAddChatUserRequest(q);
        return await Handle(authKeyId, current);
    }

    private static TLBytes ToCurrentAddChatUserRequest(TLBytes q)
    {
        var sent = new TL.layer133.messages.MessagesAddChatUser(q.AsSpan());
        using var current = AddChatUser.Builder()
            .ChatId(sent.ChatId)
            .UserId(sent.UserId)
            .FwdLimit(sent.FwdLimit)
            .Build();
        return current.TLBytes!.Value;
    }

    [TLFunction(Constructors.baseLayer_AddChatUser)]
    public async Task<TLInvitedUsers> Handle(long authKeyId, TLBytes q)
        {
            var request = (AddChatUser)q;
            long chatId = request.ChatId;
            int fwdLimit = request.FwdLimit;

            var (context, error) = await PrepareBasicChatMutation(authKeyId, chatId, requireAdmin: false);
            if (error != null)
            {
                return ErrorInvitedUsers(error);
            }

            TLChatParticipantInfo? newParticipant = null;
            try
            {
                if (!IsBasicChatAdmin(context.ActiveParticipants, context.CurrentUserId) &&
                    ChatRights.DefaultBans(context.ChatBytes, ChatBannedAction.InviteUsers))
                {
                    return ErrorInvitedUsers("CHAT_ADMIN_REQUIRED");
                }

                long? targetUserId = ResolveInputUserId(((AddChatUser)q).Get_UserIdView(),
                    context.CurrentUserId);
                if (targetUserId is not > 0 || !AllUsersExist(new[] { targetUserId.Value }))
                {
                    return ErrorInvitedUsers("USER_ID_INVALID");
                }

                long targetId = targetUserId.Value;
                foreach (var participantInfo in context.ActiveParticipants)
                {
                    if (participantInfo.AsChatParticipantInfo().UserId == targetId)
                    {
                        return ErrorInvitedUsers("USER_ALREADY_PARTICIPANT");
                    }
                }

                int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
                if (!await _privacy.IsChatInviteAllowed(context.CurrentUserId, targetId))
                {
                    _log.Debug($"👥 AddChatUser user:{context.CurrentUserId} chat:{chatId} " +
                               $"target:{targetId} blocked by privacy");
                    return BuildPrivacyBlockedInvitedUsers(context.CurrentUserId, targetId, context.ChatBytes, date);
                }

                newParticipant = ChatParticipantInfo.Builder()
                    .ChatId(chatId)
                    .UserId(targetId)
                    .Role((int)ChatParticipantRole.Member)
                    .InviterId(context.CurrentUserId)
                    .Date(date)
                    .Build();
                _chatParticipantsRepository.PutParticipant(newParticipant.Value);

                byte[] updatedChatBytes = _chatRows.UpdateStoredChatMembership(context.ChatBytes, 1);
                int newVersion = ReadChatVersion(updatedChatBytes);

                await CopyRecentChatHistory(context.CurrentUserId, targetId, chatId, fwdLimit);

                var fanoutParticipants = new List<TLChatParticipantInfo>(context.ActiveParticipants)
                {
                    newParticipant.Value
                };
                byte[] actionBytes;
                {
                    var actionUsers = new VectorOfLong();
                    actionUsers.Append(targetId);
                    using TLMessageAction action = MessageActionChatAddUser.Builder()
                        .Users(actionUsers)
                        .Build();
                    actionBytes = action.AsSpan().ToArray();
                }

                byte[] participantsUpdateBytes =
                    BuildChatParticipantsUpdateBytes(chatId, fanoutParticipants, newVersion);
                _log.Debug($"👥 AddChatUser user:{context.CurrentUserId} chat:{chatId} " +
                           $"target:{targetId} fwdLimit:{fwdLimit}");
                using TLUpdates updates = await EmitBasicGroupServiceUpdates(authKeyId,
                    context.CurrentUserId, chatId, fanoutParticipants, actionBytes,
                    updatedChatBytes, participantsUpdateBytes);
                return InvitedUsers.Builder()
                    .Updates(updates.AsSpan())
                    .MissingInvitees(new Vector())
                    .Build();
            }
            finally
            {
                newParticipant?.Dispose();
                DisposeParticipants(context.ActiveParticipants);
            }
        }

}
