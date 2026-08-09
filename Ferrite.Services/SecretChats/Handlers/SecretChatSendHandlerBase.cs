// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.SecretChats.Handlers;

public abstract class SecretChatSendHandlerBase : SecretChatHandlerBase
{
    private readonly ISecretChatsRepository _secretChatsRepository;

    private readonly ISecretChatQtsQueue _qtsQueue;
    private readonly IMTProtoTime _time;

    protected SecretChatSendHandlerBase(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, ISecretChatsRepository secretChatsRepository, IUserRepository userRepository,
        ISecretChatQtsQueue qtsQueue, SecretChatLimits limits, IMTProtoTime time)
        : base(unitOfWork, blockedPeersRepository, authorizationRepository, secretChatsRepository, userRepository, limits)
    {
        _secretChatsRepository = secretChatsRepository;

        _qtsQueue = qtsQueue;
        _time = time;
    }

    protected async ValueTask<TLSentEncryptedMessage> SendAsync(long authKeyId,
        int chatId, long accessHash, long randomId, byte[] data, bool service)
    {
        SecretChatSendPreparation preparation = await PrepareSendAsync(authKeyId,
            chatId, accessHash, randomId, data);
        if (!preparation.Ready)
        {
            return FromCompleted(preparation);
        }
        int date = CurrentDate;
        byte[] resultBytes;
        using (TLSentEncryptedMessage sent = SentEncryptedMessage.Builder()
                   .Date(date).Build())
        {
            resultBytes = sent.AsSpan().ToArray();
        }

        TLEncryptedMessage message;
        if (service)
        {
            message = EncryptedMessageService.Builder()
                .RandomId(randomId)
                .ChatId(chatId)
                .Date(date)
                .Bytes(data)
                .Build();
        }
        else
        {
            using TLEncryptedFile emptyFile = EncryptedFileEmpty.Builder().Build();
            message = EncryptedMessage.Builder()
                .RandomId(randomId)
                .ChatId(chatId)
                .Date(date)
                .Bytes(data)
                .File(emptyFile.AsSpan())
                .Build();
        }

        return await EnqueuePreparedAsync(authKeyId, preparation.Context, chatId,
            accessHash, randomId, date, message, resultBytes);
    }

    protected async ValueTask<SecretChatSendPreparation> PrepareSendAsync(
        long authKeyId, int chatId, long accessHash, long randomId, byte[] data)
    {
        if (data.Length > Limits.MaxEncryptedMessageBytes)
        {
            return Completed(Error(400, "DATA_TOO_LONG"u8));
        }
        if (data.Length < Limits.MinEncryptedMessageBytes ||
            (data.Length - 24) % 16 != 0)
        {
            return Completed(Error(400, "DATA_INVALID"u8));
        }

        SecretChatPeerResolution resolved = await ResolveActivePeerAsync(authKeyId,
            chatId, accessHash);
        if (resolved.Status != SecretChatPeerResolutionStatus.Resolved)
        {
            return Completed(resolved.Status switch
            {
                SecretChatPeerResolutionStatus.Declined =>
                    Error(400, "ENCRYPTION_DECLINED"u8),
                SecretChatPeerResolutionStatus.UserDeleted =>
                    Error(403, "USER_DELETED"u8),
                SecretChatPeerResolutionStatus.UserBlocked =>
                    Error(403, "USER_IS_BLOCKED"u8),
                _ => Error(400, "CHAT_ID_INVALID"u8)
            });
        }

        int minimumReceiptDate = checked(CurrentDate -
            Limits.DedupRetentionSeconds);
        TLDto.TLSecretChatSendReceipt? receipt = await _secretChatsRepository.GetSendReceiptAsync(chatId, authKeyId,
                randomId, minimumDate: minimumReceiptDate);
        if (receipt is not null)
        {
            using TLDto.TLSecretChatSendReceipt owned = receipt.Value;
            return new SecretChatSendPreparation(default,
                owned.AsSecretChatSendReceipt().Result.ToArray());
        }
        return new SecretChatSendPreparation(resolved.Context, null);
    }

    protected async ValueTask<TLSentEncryptedMessage> EnqueuePreparedAsync(
        long authKeyId, SecretChatPeerContext context, int chatId, long accessHash,
        long randomId, int date, TLEncryptedMessage message,
        ReadOnlyMemory<byte> result)
    {
        SecretChatSendAppendResult append;
        try
        {
            append = await _qtsQueue.EnqueueSendAsync(authKeyId,
                context.PeerAuthKeyId, context.PeerUserId, chatId, accessHash,
                randomId, date, message, result);
        }
        catch (IOException)
        {
            return Error(500, "MSG_WAIT_FAILED"u8);
        }

        using (append.Entry)
        using (append.Receipt)
        {
            if ((append.Status == SecretChatSendAppendStatus.Appended ||
                 append.Status == SecretChatSendAppendStatus.AlreadyExists) &&
                append.Receipt is not null)
            {
                byte[] original = append.Receipt.Value.AsSecretChatSendReceipt()
                    .Result.ToArray();
                return new TLSentEncryptedMessage(original, 0, original.Length);
            }

            return append.Status switch
            {
                SecretChatSendAppendStatus.NotActive =>
                    Error(400, "ENCRYPTION_DECLINED"u8),
                SecretChatSendAppendStatus.EventLimitExceeded or
                    SecretChatSendAppendStatus.ByteLimitExceeded =>
                    Error(500, "MSG_WAIT_FAILED"u8),
                SecretChatSendAppendStatus.AuthKeyRevoked =>
                    Error(400, "AUTH_KEY_INVALID"u8),
                _ => Error(400, "CHAT_ID_INVALID"u8)
            };
        }
    }

    protected int CurrentDate => checked((int)_time.GetUnixTimeInSeconds());

    protected static TLSentEncryptedMessage FromCompleted(
        SecretChatSendPreparation preparation)
    {
        byte[] bytes = preparation.CompletedResult!;
        return new TLSentEncryptedMessage(bytes, 0, bytes.Length);
    }

    protected static TLSentEncryptedMessage Error(int code,
        ReadOnlySpan<byte> message) =>
        (TLSentEncryptedMessage)RpcErrorGenerator.GenerateError(code, message);

    private static SecretChatSendPreparation Completed(
        TLSentEncryptedMessage result)
    {
        using (result)
        {
            return new SecretChatSendPreparation(default,
                result.AsSpan().ToArray());
        }
    }

    protected readonly record struct SecretChatSendPreparation(
        SecretChatPeerContext Context, byte[]? CompletedResult)
    {
        public bool Ready => CompletedResult is null;
    }
}
