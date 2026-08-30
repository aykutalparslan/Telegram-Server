// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class EditAdminHandler : ChannelsHandlerBase
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IUserRepository _userRepository;

    public EditAdminHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _chatParticipantsRepository = chatParticipantsRepository;
        _userRepository = userRepository;

    }

    [TLFunction(Constructors.baseLayer_EditAdmin)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        long? channelId = ResolveInputChannelId(((EditAdmin)q).Get_ChannelView());
        byte[] requestedRights = ((EditAdmin)q).AdminRights.ToArray();
        byte[] rank = ((EditAdmin)q).Rank.ToArray();

        var (currentUserId, channelBytes, error) = await PrepareChannelMutation(authKeyId,
            channelId, creatorOnly: false, ChatAdminRightRequirement.AddAdmins);
        if (error != null)
        {
            return error.Value;
        }
        if (rank.Length > 16)
        {
            return ErrorUpdates("ADMIN_RANK_INVALID"u8);
        }

        long id = channelId!.Value;
        long? targetUserId = ResolveInputUserId(((EditAdmin)q).Get_UserIdView(), currentUserId);
        if (targetUserId is not > 0)
        {
            return ErrorUpdates("USER_ID_INVALID"u8);
        }

        using var caller = await _chatParticipantsRepository
            .GetParticipantAsync(id, currentUserId);
        using var target = await _chatParticipantsRepository
            .GetParticipantAsync(id, targetUserId.Value);
        bool targetJoinsOnPromotion = false;
        if (target == null || !IsActiveParticipant(target.Value))
        {
            bool targetKicked = target != null &&
                target.Value.AsChatParticipantInfo().Role == (int)ChatParticipantRole.Banned;
            if (targetKicked)
            {
                return ErrorUpdates("USER_BANNED_IN_CHANNEL"u8);
            }
            if (!ChatRights.HasAnyAdminRight(requestedRights))
            {
                return ErrorUpdates("USER_NOT_PARTICIPANT"u8);
            }
            targetJoinsOnPromotion = true;
        }

        int targetRole = 0;
        long targetInviterId = 0;
        int targetDate = 0;
        if (target != null)
        {
            var targetInfo = target.Value.AsChatParticipantInfo();
            targetRole = targetInfo.Role;
            targetInviterId = targetInfo.InviterId;
            targetDate = targetInfo.Date;
        }
        if (targetRole == (int)ChatParticipantRole.Creator)
        {
            return ErrorUpdates("USER_CREATOR"u8);
        }

        bool callerIsCreator = caller != null &&
            caller.Value.AsChatParticipantInfo().Role == (int)ChatParticipantRole.Creator;
        if (!callerIsCreator && targetRole == (int)ChatParticipantRole.Admin &&
            targetInviterId != currentUserId)
        {
            return ErrorUpdates("CHAT_ADMIN_REQUIRED"u8);
        }

        bool promote = ChatRights.HasAnyAdminRight(requestedRights);
        if (promote && caller != null &&
            ChatRights.GrantsBeyondCaller(requestedRights, caller.Value))
        {
            return ErrorUpdates("RIGHT_FORBIDDEN"u8);
        }
        if (targetJoinsOnPromotion)
        {
            using var targetUser = _userRepository.GetUser(targetUserId.Value);
            if (targetUser == null)
            {
                return ErrorUpdates("USER_ID_INVALID"u8);
            }
        }

        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        byte[] previousParticipant = target != null
            ? BuildChannelParticipantBytes(target.Value, currentUserId)
            : BuildLeftParticipantBytes(targetUserId.Value);
        byte[] newParticipant;
        using (TLChatParticipantInfo updated = BuildParticipantRow(id, targetUserId.Value,
                   promote ? ChatParticipantRole.Admin : ChatParticipantRole.Member,
                   promote ? currentUserId : targetInviterId,
                   promote ? date : targetDate,
                   promote ? requestedRights : null,
                   bannedRights: null,
                   promote && rank.Length > 0 ? rank : null))
        {
            _chatParticipantsRepository.PutParticipant(updated);
            newParticipant = BuildChannelParticipantBytes(updated, currentUserId);
        }

        byte[] logAction;
        using (TLChannelAdminLogEventAction action =
               ChannelAdminLogEventActionParticipantToggleAdmin.Builder()
                   .PrevParticipant(previousParticipant)
                   .NewParticipant(newParticipant)
                   .Build())
        {
            logAction = action.AsSpan().ToArray();
        }
        await AppendAdminLogEventAsync(id, currentUserId, logAction, date,
            ReadUserSearchText(targetUserId.Value));

        byte[] resultChannelBytes = channelBytes;
        byte[]? joinMessageBytes = null;
        int joinMessagePts = 0;
        if (targetJoinsOnPromotion)
        {
            resultChannelBytes = _chatRows.UpdateStoredChannelParticipantsCount(channelBytes, 1);
            bool megagroup;
            {
                using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
                megagroup = stored.AsChannel().Megagroup;
            }
            if (megagroup)
            {
                byte[] actionBytes;
                {
                    var actionUsers = new VectorOfLong();
                    actionUsers.Append(targetUserId.Value);
                    using TLMessageAction action = MessageActionChatAddUser.Builder()
                        .Users(actionUsers)
                        .Build();
                    actionBytes = action.AsSpan().ToArray();
                }
                (joinMessageBytes, joinMessagePts) =
                    await WriteChannelServiceMessage(id, currentUserId, actionBytes, date);
            }
        }

        _log.Debug($"📣 EditAdmin user:{currentUserId} channel:{id} " +
                   $"target:{targetUserId.Value} promote:{promote} " +
                   $"joined:{targetJoinsOnPromotion}");

        var result = await BuildChannelUpdates(authKeyId, currentUserId, resultChannelBytes,
            new[] { targetUserId.Value });
        if (joinMessageBytes != null)
        {
            await _fanout.PushChannelServiceMessageAsync(id, currentUserId, joinMessageBytes,
                joinMessagePts);
        }
        await _fanout.EnqueueUpdateChannelAsync(targetUserId.Value, id);
        return result;
    }
}
