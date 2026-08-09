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

public sealed class EditChatAdminHandler : MessagesHandlerBase
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    public EditChatAdminHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
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

    [TLFunction(Constructors.baseLayer_EditChatAdmin)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
        {
            long chatId = ((EditChatAdmin)q).ChatId;
            bool isAdmin = ((EditChatAdmin)q).IsAdmin;

            var (context, error) = await PrepareBasicChatMutation(authKeyId, chatId,
                requireAdmin: true, requireCreator: true);
            if (error != null)
            {
                return ErrorBool(error);
            }

            try
            {
                long? targetUserId = ResolveInputUserId(((EditChatAdmin)q).Get_UserIdView(),
                    context.CurrentUserId);
                if (targetUserId is not > 0)
                {
                    return ErrorBool("USER_ID_INVALID");
                }

                long targetId = targetUserId.Value;
                int targetRole = 0;
                long targetInviterId = 0;
                int targetDate = 0;
                foreach (var participantInfo in context.ActiveParticipants)
                {
                    var info = participantInfo.AsChatParticipantInfo();
                    if (info.UserId == targetId)
                    {
                        targetRole = info.Role;
                        targetInviterId = info.InviterId;
                        targetDate = info.Date;
                        break;
                    }
                }
                if (targetRole == 0)
                {
                    return ErrorBool("USER_NOT_PARTICIPANT");
                }
                if (targetRole == (int)ChatParticipantRole.Creator)
                {
                    return ErrorBool("USER_ID_INVALID");
                }

                int newRole = isAdmin
                    ? (int)ChatParticipantRole.Admin
                    : (int)ChatParticipantRole.Member;
                if (targetRole == newRole)
                {
                    return BoolTrue.Builder().Build();
                }

                TLChatParticipantInfo updatedRow = ChatParticipantInfo.Builder()
                    .ChatId(chatId)
                    .UserId(targetId)
                    .Role(newRole)
                    .InviterId(targetInviterId)
                    .Date(targetDate)
                    .Build();
                try
                {
                    _chatParticipantsRepository.PutParticipant(updatedRow);
                    byte[] updatedChatBytes = _chatRows.UpdateStoredChatMembership(context.ChatBytes, 0);
                    int newVersion = ReadChatVersion(updatedChatBytes);
                    await _unitOfWork.SaveAsync();

                    // Push the changed participant list (with the bumped chat version) to
                    // every active member; the Bool result itself carries no state.
                    var refreshed = new List<TLChatParticipantInfo>(context.ActiveParticipants.Count);
                    foreach (var participantInfo in context.ActiveParticipants)
                    {
                        refreshed.Add(participantInfo.AsChatParticipantInfo().UserId == targetId
                            ? updatedRow
                            : participantInfo);
                    }
                    byte[] participantsUpdateBytes =
                        BuildChatParticipantsUpdateBytes(chatId, refreshed, newVersion);
                    foreach (var participantInfo in context.ActiveParticipants)
                    {
                        long participantId = participantInfo.AsChatParticipantInfo().UserId;
                        await _updates.EnqueueUpdate(participantId,
                            new TLUpdate(participantsUpdateBytes, 0, participantsUpdateBytes.Length));
                    }
                }
                finally
                {
                    updatedRow.Dispose();
                }

                _log.Debug($"👥 EditChatAdmin user:{context.CurrentUserId} chat:{chatId} " +
                           $"target:{targetId} isAdmin:{isAdmin}");
                return BoolTrue.Builder().Build();
            }
            finally
            {
                DisposeParticipants(context.ActiveParticipants);
            }
        }
}
