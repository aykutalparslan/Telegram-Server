// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.SecretChats.Handlers;

public sealed class DiscardEncryptionHandler : SecretChatLifecycleHandlerBase
{
    private readonly ISecretChatsRepository _secretChatsRepository;

    private readonly IMTProtoTime _time;

    public DiscardEncryptionHandler(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, ISecretChatsRepository secretChatsRepository,
        ISecretChatDeviceSelector deviceSelector,
        SecretChatControlDelivery controlDelivery, SecretChatLimits limits,
        IMTProtoTime time, SecretChatTelemetry? telemetry = null)
        : base(unitOfWork, blockedPeersRepository, authorizationRepository, secretChatsRepository, deviceSelector, controlDelivery, limits, telemetry)
    {
        _secretChatsRepository = secretChatsRepository;

        _time = time;
    }

    [TLFunction(Constructors.baseLayer_DiscardEncryption)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = (DiscardEncryption)q;
        int chatId = request.ChatId;
        bool deleteHistory = request.DeleteHistory;

        if (chatId == 0)
        {
            return Error("CHAT_ID_EMPTY"u8);
        }
        if (chatId < 0)
        {
            return Error("ENCRYPTION_ID_INVALID"u8);
        }
        if (await GetCurrentUserIdAsync(authKeyId) is null)
        {
            return Error("AUTH_KEY_INVALID"u8);
        }

        int date = checked((int)_time.GetUnixTimeInSeconds());
        SecretChatDiscardResult result = await _secretChatsRepository
            .TryDiscardChatAsync(chatId, authKeyId, deleteHistory, date);
        if (result.Status == SecretChatDiscardStatus.Discarded &&
            result.Chat is not null)
        {
            using TLDto.TLSecretChatState discarded = result.Chat.Value;
            bool durable = await EnsureDiscardedControlsAsync(discarded, authKeyId,
                result.NotificationAuthKeyIds);
            await CompleteControlTransitionAsync(discarded, durable);
            Telemetry?.Transition(authKeyId, chatId,
                SecretChatPersistenceState.Discarded.ToString());
            return BoolTrue.Builder().Build();
        }

        if (result.Chat is TLDto.TLSecretChatState resultChat)
        {
            using (resultChat)
            {
                if (result.Status == SecretChatDiscardStatus.AlreadyDiscarded)
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

        Telemetry?.Rejection("discard_encryption", authKeyId, chatId,
            result.Status.ToString());
        return result.Status switch
        {
            SecretChatDiscardStatus.AlreadyAccepted =>
                Error("ENCRYPTION_ALREADY_ACCEPTED"u8),
            SecretChatDiscardStatus.AlreadyDiscarded =>
                Error("ENCRYPTION_ALREADY_DECLINED"u8),
            _ => Error("ENCRYPTION_ID_INVALID"u8)
        };
    }

    private static TLBool Error(ReadOnlySpan<byte> message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400, message);
}
