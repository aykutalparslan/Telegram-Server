// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Crypto;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.SecretChats.Handlers;

public sealed class RequestEncryptionHandler : SecretChatLifecycleHandlerBase
{
    private readonly ISecretChatsRepository _secretChatsRepository;
    private readonly IUserRepository _userRepository;

    private readonly IRandomGenerator _random;
    private readonly IMTProtoTime _time;

    public RequestEncryptionHandler(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, ISecretChatsRepository secretChatsRepository, IUserRepository userRepository,
        ISecretChatDeviceSelector deviceSelector,
        SecretChatControlDelivery controlDelivery, SecretChatLimits limits,
        IRandomGenerator random, IMTProtoTime time,
        SecretChatTelemetry? telemetry = null)
        : base(unitOfWork, blockedPeersRepository, authorizationRepository, secretChatsRepository, deviceSelector, controlDelivery, limits, telemetry)
    {
        _secretChatsRepository = secretChatsRepository;
        _userRepository = userRepository;

        _random = random;
        _time = time;
    }

    [TLFunction(Constructors.baseLayer_RequestEncryption)]
    public async ValueTask<TLEncryptedChat> Handle(long authKeyId, TLBytes q)
    {
        long targetUserId;
        long? targetAccessHash;
        bool targetsSelf;
        var request = (RequestEncryption)q;
        int randomId = request.RandomId;
        byte[] gA = request.GA.ToArray();
        InputUserView target = request.Get_UserIdView();
        if (target.Is(out InputUserSelf _))
        {
            targetsSelf = true;
            targetUserId = 0;
            targetAccessHash = null;
        }
        else if (target.Is(out InputUser inputUser))
        {
            targetsSelf = false;
            targetUserId = inputUser.UserId;
            targetAccessHash = inputUser.AccessHash;
        }
        else
        {
            targetsSelf = false;
            targetUserId = 0;
            targetAccessHash = null;
        }

        long? initiatorUserIdValue = await GetCurrentUserIdAsync(authKeyId);
        if (initiatorUserIdValue is not long initiatorUserId)
        {
            return (TLEncryptedChat)RpcErrorGenerator.GenerateError(400,
                "AUTH_KEY_INVALID"u8);
        }
        if (targetsSelf || targetUserId <= 0 || targetUserId == initiatorUserId)
        {
            return Error(400, "USER_ID_INVALID"u8);
        }
        if (randomId <= 0)
        {
            return Error(400, "RANDOM_ID_INVALID"u8);
        }
        if (!TelegramDhParameters.IsValidSecretChatPublicValue(gA))
        {
            return Error(400, "DH_G_A_INVALID"u8);
        }

        TLUser? targetValue = _userRepository.GetUser(targetUserId);
        if (targetValue is null)
        {
            return Error(400, "USER_ID_INVALID"u8);
        }
        using (TLUser targetOwned = targetValue.Value)
        {
            if (targetOwned.Constructor != Constructors.baseLayer_User)
            {
                return Error(400, "USER_ID_INVALID"u8);
            }
            User targetUser = targetOwned.AsUser();
            if (targetUser.Deleted)
            {
                return Error(400, "INPUT_USER_DEACTIVATED"u8);
            }
            if (targetUser.Bot || targetUser.Id != targetUserId ||
                targetAccessHash is long accessHash &&
                (!targetUser.Flags[0] || targetUser.AccessHash != accessHash))
            {
                return Error(400, "USER_ID_INVALID"u8);
            }
        }

        if (IsBlockedBy(targetUserId, initiatorUserId) ||
            IsBlockedBy(initiatorUserId, targetUserId))
        {
            return Error(403, "USER_IS_BLOCKED"u8);
        }

        IReadOnlyList<long> requestedAuthKeyIds = await DeviceSelector
            .GetEligibleAuthKeyIds(targetUserId);
        if (requestedAuthKeyIds.Count == 0)
        {
            return Error(400, "USER_ID_INVALID"u8);
        }

        int date = checked((int)_time.GetUnixTimeInSeconds());
        long accessHashValue = _random.NextLong();
        if (accessHashValue == 0)
        {
            accessHashValue = 1;
        }
        var keys = new VectorOfLong();
        foreach (long requestedAuthKeyId in requestedAuthKeyIds.Distinct())
        {
            keys.Append(requestedAuthKeyId);
        }
        using TLDto.TLSecretChatState pending = TLDto.SecretChatState.Builder()
            .ChatId(randomId)
            .AccessHash(accessHashValue)
            .InitiatorUserId(initiatorUserId)
            .RecipientUserId(targetUserId)
            .InitiatorAuthKeyId(authKeyId)
            .State((int)SecretChatPersistenceState.Pending)
            .CreatedAt(date)
            .UpdatedAt(date)
            .GA(gA)
            .InitiatorReadMaxDate(0)
            .RecipientReadMaxDate(0)
            .RequestedRecipientAuthKeyIds(keys)
            .Build();
        SecretChatCreateResult result = await _secretChatsRepository
            .TryCreateChatAsync(pending, Limits.MaxPendingChatsPerAuthKey,
                Limits.MaxOutstandingRequestsPerAuthKey);

        if ((result.Status is SecretChatCreateStatus.Created or
             SecretChatCreateStatus.Idempotent) && result.Chat is not null)
        {
            using TLDto.TLSecretChatState stored = result.Chat.Value;
            TLDto.SecretChatState row = stored.AsSecretChatState();
            int storedChatId = row.ChatId;
            long storedAccessHash = row.AccessHash;
            int storedDate = row.CreatedAt;
            long storedInitiatorUserId = row.InitiatorUserId;
            long storedRecipientUserId = row.RecipientUserId;
            SecretChatPersistenceState state = (SecretChatPersistenceState)row.State;
            bool controlsDurable;
            if (state == SecretChatPersistenceState.Pending)
            {
                controlsDurable = await EnsureRequestedControlsAsync(stored);
            }
            else if (state == SecretChatPersistenceState.Active)
            {
                controlsDurable = await EnsureAcceptedControlsAsync(stored);
            }
            else
            {
                IReadOnlyList<long> notificationAuthKeyIds =
                    await GetControlTargetAuthKeyIdsAsync(stored);
                controlsDurable = notificationAuthKeyIds.Count == 0 ||
                    await EnsureDiscardedControlsAsync(stored, authKeyId,
                        notificationAuthKeyIds);
            }
            await CompleteControlTransitionAsync(stored, controlsDurable);
            Telemetry?.Transition(authKeyId, storedChatId, state.ToString());
            return BuildWaiting(storedChatId, storedAccessHash, storedDate,
                storedInitiatorUserId, storedRecipientUserId);
        }

        DisposeChat(result.Chat);
        Telemetry?.Rejection("request_encryption", authKeyId, randomId,
            result.Status.ToString());
        return result.Status switch
        {
            SecretChatCreateStatus.ChatIdCollision or
                SecretChatCreateStatus.InitiatorRandomIdCollision =>
                Error(400, "RANDOM_ID_DUPLICATE"u8),
            SecretChatCreateStatus.PairRequestExists =>
                Error(400, "ENCRYPTION_REQUEST_ALREADY_SENT"u8),
            SecretChatCreateStatus.PendingLimitExceeded =>
                Error(400, "ENCRYPTION_CHATS_TOO_MUCH"u8),
            SecretChatCreateStatus.RecipientRequestLimitExceeded =>
                Error(400, "ENCRYPTION_REQUESTS_TOO_MUCH"u8),
            SecretChatCreateStatus.AuthKeyRevoked =>
                Error(400, "AUTH_KEY_INVALID"u8),
            _ => Error(400, "USER_ID_INVALID"u8)
        };
    }

    private static TLEncryptedChat Error(int code, ReadOnlySpan<byte> message) =>
        (TLEncryptedChat)RpcErrorGenerator.GenerateError(code, message);

    private static void DisposeChat(TLDto.TLSecretChatState? chat)
    {
        if (chat is TLDto.TLSecretChatState value)
        {
            value.Dispose();
        }
    }
}
