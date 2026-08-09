// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

/// <summary>
/// Transfers a channel's ownership, re-authenticated through the SRP
/// path. This is the only method here that can hand a channel away, so its
/// refusals matter more than its success.
///
/// TWO td_api entry points share it and THE FIRST ONE MUST FAIL.
/// `td_api::canTransferOwnership` (`Requests.cpp:5619`) probes through
/// `CanEditChannelCreatorQuery` (`DialogParticipantManager.cpp:714`) with
/// `inputChannelEmpty` + `inputCheckPasswordEmpty`, and `can_transfer_ownership`
/// asserts `CHECK(r_result.is_error())` (`DialogParticipantManager.cpp:2954`):
/// answering that form with `Updates` ABORTS pinned TDLib rather than failing a
/// request. It reads only `PASSWORD_HASH_INVALID`, `PASSWORD_MISSING`,
/// `PASSWORD_TOO_FRESH_&lt;n&gt;` and `SESSION_TOO_FRESH_&lt;n&gt;`
/// (`:2957-2979`). So the password is checked FIRST, before the channel is even
/// resolved, and an empty password on a channel-less probe answers
/// `PASSWORD_MISSING` or `PASSWORD_HASH_INVALID` by construction.
///
/// DELIBERATE DEVIATION: Ferrite issues neither `PASSWORD_TOO_FRESH_&lt;n&gt;`
/// nor `SESSION_TOO_FRESH_&lt;n&gt;`. Telegram's 24-hour cool-down after a
/// password change or a new login exists to blunt an account takeover on the
/// public network; on a self-hosted deployment it would only make a transfer
/// unreachable for a day after 2FA is set, which is exactly when an operator
/// sets one up. The proof itself is still mandatory.
/// </summary>
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
        // A zero self-id refuses `inputUserSelf`, which is what transferring a
        // channel to yourself is.
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

        // The new owner has to already be in the channel: ownership is a role
        // change, not an invitation, and a non-member owner would leave the
        // channel with a creator its participant list does not contain.
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
        // The outgoing owner keeps full administrative rights, promoted by the
        // new owner: demoting them to an ordinary member would lock the person
        // performing the transfer out of the channel they just handed over.
        using (TLChatParticipantInfo previousCreator = BuildParticipantRow(id,
                   currentUserId, ChatParticipantRole.Admin, targetUserId, date,
                   fullRights, null, null))
        {
            _chatParticipantsRepository.PutParticipant(previousCreator);
        }

        Ferrite.TL.baseLayer.TLUpdates result = await BuildChannelUpdates(authKeyId,
            currentUserId, channelBytes, [targetUserId]);
        // Both role rows changed, so EVERY member's cached channelFull is stale,
        // not merely the two participants'.
        await _fanout.PushUpdateChannelToOtherMembersAsync(id, currentUserId);
        _log.Debug($"📣 EditCreator user:{currentUserId} channel:{id} " +
                   $"newCreator:{targetUserId}");
        return result;
    }
}
