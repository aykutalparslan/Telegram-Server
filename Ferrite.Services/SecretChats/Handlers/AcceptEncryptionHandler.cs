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

public sealed class AcceptEncryptionHandler : SecretChatLifecycleHandlerBase
{
    private readonly ISecretChatsRepository _secretChatsRepository;

    private readonly IMTProtoTime _time;

    public AcceptEncryptionHandler(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, ISecretChatsRepository secretChatsRepository,
        ISecretChatDeviceSelector deviceSelector,
        SecretChatControlDelivery controlDelivery, SecretChatLimits limits,
        IMTProtoTime time, SecretChatTelemetry? telemetry = null)
        : base(unitOfWork, blockedPeersRepository, authorizationRepository, secretChatsRepository, deviceSelector, controlDelivery, limits, telemetry)
    {
        _secretChatsRepository = secretChatsRepository;

        _time = time;
    }

    [TLFunction(Constructors.baseLayer_AcceptEncryption)]
    public async ValueTask<TLEncryptedChat> Handle(long authKeyId, TLBytes q)
    {
        var request = (AcceptEncryption)q;
        InputEncryptedChatView peerView = request.Get_PeerView();
        if (!peerView.Is(out InputEncryptedChat peer))
        {
            return Error("CHAT_ID_INVALID"u8);
        }
        int chatId = peer.ChatId;
        long accessHash = peer.AccessHash;
        byte[] gB = request.GB.ToArray();
        long keyFingerprint = request.KeyFingerprint;

        long? recipientUserIdValue = await GetCurrentUserIdAsync(authKeyId);
        if (recipientUserIdValue is not long recipientUserId)
        {
            return Error("CHAT_ID_INVALID"u8);
        }
        if (chatId <= 0)
        {
            return Error("CHAT_ID_INVALID"u8);
        }
        if (!TelegramDhParameters.IsValidSecretChatPublicValue(gB))
        {
            return Error("DH_G_B_INVALID"u8);
        }

        TLDto.TLSecretChatState? existingValue = await _secretChatsRepository.GetChatAsync(chatId);
        if (existingValue is null)
        {
            return Error("CHAT_ID_INVALID"u8);
        }
        using (TLDto.TLSecretChatState existing = existingValue.Value)
        {
            TLDto.SecretChatState row = existing.AsSecretChatState();
            if (row.AccessHash != accessHash ||
                row.RecipientUserId != recipientUserId)
            {
                return Error("CHAT_ID_INVALID"u8);
            }
            bool wasRequested = row.RequestedRecipientAuthKeyIds.ToArray()
                .Contains(authKeyId);
            bool isBoundRecipient = row.Flags[1] &&
                                    row.RecipientAuthKeyId == authKeyId;
            if (!wasRequested && !isBoundRecipient)
            {
                return Error("CHAT_ID_INVALID"u8);
            }
            SecretChatPersistenceState state =
                (SecretChatPersistenceState)row.State;
            if (state == SecretChatPersistenceState.Discarded)
            {
                IReadOnlyList<long> targets =
                    await GetControlTargetAuthKeyIdsAsync(existing);
                bool durable = targets.Count == 0 ||
                    await EnsureDiscardedControlsAsync(existing, authKeyId, targets);
                await CompleteControlTransitionAsync(existing, durable);
                return Error("ENCRYPTION_ALREADY_DECLINED"u8);
            }
            if (state == SecretChatPersistenceState.Active)
            {
                bool durable = await EnsureAcceptedControlsAsync(existing);
                await CompleteControlTransitionAsync(existing, durable);
                return Error("ENCRYPTION_ALREADY_ACCEPTED"u8);
            }
        }

        IReadOnlyList<long> eligibleAuthKeyIds = await DeviceSelector
            .GetEligibleAuthKeyIds(recipientUserId);
        if (!eligibleAuthKeyIds.Contains(authKeyId))
        {
            return Error("CHAT_ID_INVALID"u8);
        }

        int date = checked((int)_time.GetUnixTimeInSeconds());
        SecretChatAcceptResult result = await _secretChatsRepository
            .TryAcceptChatAsync(chatId, authKeyId, gB, keyFingerprint, date,
                Limits.MaxActiveChatsPerAuthKey);
        if (result.Status == SecretChatAcceptStatus.Accepted &&
            result.Chat is not null)
        {
            using TLDto.TLSecretChatState accepted = result.Chat.Value;
            bool durable = await EnsureAcceptedControlsAsync(accepted);
            await CompleteControlTransitionAsync(accepted, durable);
            TLDto.SecretChatState row = accepted.AsSecretChatState();
            Telemetry?.Transition(authKeyId, row.ChatId,
                SecretChatPersistenceState.Active.ToString());
            return BuildActive(row.ChatId, row.AccessHash, row.CreatedAt,
                row.InitiatorUserId, row.RecipientUserId, row.GA,
                row.KeyFingerprint);
        }

        if (result.Chat is TLDto.TLSecretChatState resultChat)
        {
            using (resultChat)
            {
                SecretChatPersistenceState state = (SecretChatPersistenceState)
                    resultChat.AsSecretChatState().State;
                if (state == SecretChatPersistenceState.Active)
                {
                    bool durable = await EnsureAcceptedControlsAsync(resultChat);
                    await CompleteControlTransitionAsync(resultChat, durable);
                    return Error("ENCRYPTION_ALREADY_ACCEPTED"u8);
                }
                if (state == SecretChatPersistenceState.Discarded)
                {
                    IReadOnlyList<long> targets =
                        await GetControlTargetAuthKeyIdsAsync(resultChat);
                    bool durable = targets.Count == 0 ||
                        await EnsureDiscardedControlsAsync(resultChat, authKeyId,
                            targets);
                    await CompleteControlTransitionAsync(resultChat, durable);
                    return Error("ENCRYPTION_ALREADY_DECLINED"u8);
                }
            }
        }

        Telemetry?.Rejection("accept_encryption", authKeyId, chatId,
            result.Status.ToString());
        return result.Status switch
        {
            SecretChatAcceptStatus.ActiveLimitExceeded =>
                Error("ENCRYPTION_CHATS_TOO_MUCH"u8),
            _ => Error("CHAT_ID_INVALID"u8)
        };
    }

    private static TLEncryptedChat Error(ReadOnlySpan<byte> message) =>
        (TLEncryptedChat)RpcErrorGenerator.GenerateError(400, message);
}
