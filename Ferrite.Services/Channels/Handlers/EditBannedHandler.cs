// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class EditBannedHandler : ChannelsHandlerBase
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    public EditBannedHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _chatParticipantsRepository = chatParticipantsRepository;

    }

    [TLFunction(Constructors.baseLayer_EditBanned)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        long? channelId = ResolveInputChannelId(((EditBanned)q).Get_ChannelView());
        byte[] requestedRights = ((EditBanned)q).BannedRights.ToArray();

        var (currentUserId, channelBytes, error) = await PrepareChannelMutation(authKeyId,
            channelId, creatorOnly: false, ChatAdminRightRequirement.BanUsers);
        if (error != null)
        {
            return error.Value;
        }

        long id = channelId!.Value;
        long? targetUserId = ResolveInputPeerUserId(((EditBanned)q).Get_ParticipantView(),
            currentUserId);
        if (targetUserId is not > 0)
        {
            return ErrorUpdates("PARTICIPANT_ID_INVALID"u8);
        }

        bool megagroup;
        {
            using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
            megagroup = stored.AsChannel().Megagroup;
        }

        using var target = await _chatParticipantsRepository
            .GetParticipantAsync(id, targetUserId.Value);
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
        if (targetRole is (int)ChatParticipantRole.Creator or (int)ChatParticipantRole.Admin)
        {
            // Admins must be demoted through channels.editAdmin before they can be
            // restricted or kicked.
            return ErrorUpdates("USER_ADMIN_INVALID"u8);
        }

        bool anyFlag = ChatRights.HasAnyBannedFlag(requestedRights);
        bool kick = anyFlag && ChatRights.BansViewMessages(requestedRights);
        bool restrict = anyFlag && !kick;
        bool wasActiveMember = targetRole == (int)ChatParticipantRole.Member;
        if (!anyFlag && target == null)
        {
            // Unbanning a user with no participant row is a no-op.
            return await BuildChannelUpdates(authKeyId, currentUserId, channelBytes,
                new[] { targetUserId.Value });
        }
        if (restrict && target == null)
        {
            return ErrorUpdates("USER_NOT_PARTICIPANT"u8);
        }

        // A view_messages ban kicks the user out (role Banned); other flags restrict a
        // member in place, or keep a kicked/left user outside with restrictions. An
        // all-false rights object unbans: a restricted member becomes a plain member,
        // a kicked user becomes left (unbanned but not re-joined).
        ChatParticipantRole newRole = kick
            ? ChatParticipantRole.Banned
            : wasActiveMember
                ? ChatParticipantRole.Member
                : ChatParticipantRole.Left;

        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        // Both sides of the admin-log action, captured around the write. Pinned
        // TDLib drops the event unless the two name the same user and both parse
        // as valid participants (`DialogEventLog.cpp:89-101`); a restricted or
        // banned row is only valid when its `kicked_by` is a real user, which the
        // shared participant builder supplies.
        byte[] previousParticipant = target != null
            ? BuildChannelParticipantBytes(target.Value, currentUserId)
            : BuildLeftParticipantBytes(targetUserId.Value);
        byte[] newParticipant;
        using (TLChatParticipantInfo updated = BuildParticipantRow(id, targetUserId.Value,
                   newRole,
                   anyFlag ? currentUserId : targetInviterId,
                   anyFlag ? date : targetDate,
                   adminRights: null,
                   anyFlag ? requestedRights : null,
                   rank: null))
        {
            _chatParticipantsRepository.PutParticipant(updated);
            newParticipant = BuildChannelParticipantBytes(updated, currentUserId);
        }

        byte[] logAction;
        using (TLChannelAdminLogEventAction action =
               ChannelAdminLogEventActionParticipantToggleBan.Builder()
                   .PrevParticipant(previousParticipant)
                   .NewParticipant(newParticipant)
                   .Build())
        {
            logAction = action.AsSpan().ToArray();
        }
        await AppendAdminLogEventAsync(id, currentUserId, logAction, date,
            ReadUserSearchText(targetUserId.Value));

        byte[] resultChannelBytes = channelBytes;
        if (kick && wasActiveMember)
        {
            resultChannelBytes = _chatRows.UpdateStoredChannelParticipantsCount(channelBytes, -1);
        }

        _log.Debug($"📣 EditBanned user:{currentUserId} channel:{id} " +
                   $"target:{targetUserId.Value} kick:{kick} restrict:{restrict}");

        if (kick && wasActiveMember && megagroup)
        {
            // Megagroups record the kick as a messageActionChatDeleteUser service
            // message, mirroring leaveChannel/deleteChatUser.
            byte[] actionBytes;
            using (TLMessageAction action = MessageActionChatDeleteUser.Builder()
                       .UserId(targetUserId.Value)
                       .Build())
            {
                actionBytes = action.AsSpan().ToArray();
            }
            var kickUpdates = await EmitChannelServiceMessage(authKeyId, currentUserId, id,
                resultChannelBytes, actionBytes);
            await _fanout.EnqueueUpdateChannelAsync(targetUserId.Value, id);
            return kickUpdates;
        }

        var result = await BuildChannelUpdates(authKeyId, currentUserId, resultChannelBytes,
            new[] { targetUserId.Value });
        await _fanout.EnqueueUpdateChannelAsync(targetUserId.Value, id);
        return result;
    }
}
