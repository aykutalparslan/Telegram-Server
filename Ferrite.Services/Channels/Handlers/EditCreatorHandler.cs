// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class EditCreatorHandler : ChannelsHandlerBase
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IUserRepository _userRepository;

    private readonly IAccountPasswordManager _passwords;

    public EditCreatorHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout, IAccountPasswordManager passwords)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _chatParticipantsRepository = chatParticipantsRepository;
        _userRepository = userRepository;

        _passwords = passwords;
    }

    [TLFunction(Constructors.baseLayer_EditCreator)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (EditCreator)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        long? requestedUserId = ResolveInputUserId(request.Get_UserIdView(), 0);
        PasswordVerificationStatus verification;
        using (TLInputCheckPasswordSRP password = request.Get_Password())
        {
            verification = await _passwords.VerifyPasswordAsync(authKeyId, password);
        }

        switch (verification)
        {
            case PasswordVerificationStatus.AuthKeyInvalid:
                return ErrorUpdates("AUTH_KEY_INVALID"u8);
            case PasswordVerificationStatus.PasswordMissing:
                return ErrorUpdates("PASSWORD_MISSING"u8);
            case PasswordVerificationStatus.ProofInvalid:
                return ErrorUpdates("PASSWORD_HASH_INVALID"u8);
        }

        var (currentUserId, channelBytes, error) =
            await PrepareChannelMutation(authKeyId, channelId, creatorOnly: true);
        if (error != null)
        {
            return error.Value;
        }
        if (requestedUserId is not > 0 || requestedUserId == currentUserId)
        {
            return ErrorUpdates("USER_ID_INVALID"u8);
        }

        long id = channelId!.Value;
        long targetUserId = requestedUserId.Value;
        using (TLUser? target = _userRepository.GetUser(targetUserId))
        {
            if (target == null)
            {
                return ErrorUpdates("USER_ID_INVALID"u8);
            }
        }

        int targetInviterId;
        int targetDate;
        using (TLChatParticipantInfo? targetParticipant = await _chatParticipantsRepository.GetParticipantAsync(id, targetUserId))
        {
            if (targetParticipant == null || !IsActiveParticipant(targetParticipant.Value))
            {
                return ErrorUpdates("USER_NOT_PARTICIPANT"u8);
            }

            var info = targetParticipant.Value.AsChatParticipantInfo();
            targetInviterId = (int)info.InviterId;
            targetDate = info.Date;
        }

        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        byte[] fullRights = BuildFullAdminRights();
        using (TLChatParticipantInfo newCreator = BuildParticipantRow(id, targetUserId,
                   ChatParticipantRole.Creator, targetInviterId, targetDate,
                   fullRights, null, null))
        {
            _chatParticipantsRepository.PutParticipant(newCreator);
        }
        using (TLChatParticipantInfo previousCreator = BuildParticipantRow(id,
                   currentUserId, ChatParticipantRole.Admin, targetUserId, date,
                   fullRights, null, null))
        {
            _chatParticipantsRepository.PutParticipant(previousCreator);
        }

        Ferrite.TL.baseLayer.TLUpdates result = await BuildChannelUpdates(authKeyId,
            currentUserId, channelBytes, [targetUserId]);
        await _fanout.PushUpdateChannelToOtherMembersAsync(id, currentUserId);
        _log.Debug($"📣 EditCreator user:{currentUserId} channel:{id} " +
                   $"newCreator:{targetUserId}");
        return result;
    }
}
