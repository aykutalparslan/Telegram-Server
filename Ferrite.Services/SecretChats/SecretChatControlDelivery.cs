// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.SecretChats;

public enum SecretChatControlKind : byte
{
    Requested = 1,
    Accepted = 2,
    LosingDeviceDiscarded = 3,
    Discarded = 4,
    Read = 5
}

public sealed class SecretChatControlDelivery
{
    private readonly ISecretChatsRepository _secretChatsRepository;

    private const int OrderingStripeCount = 256;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUpdatesService _updates;
    private readonly SecretChatLimits _limits;
    private readonly ILogger _log;
    private readonly SemaphoreSlim[] _orderingGates = CreateGates();

    public SecretChatControlDelivery(IUnitOfWork unitOfWork, ISecretChatsRepository secretChatsRepository,
        IUpdatesService updates, SecretChatLimits limits, ILogger log)
    {
        _secretChatsRepository = secretChatsRepository;

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            limits.QtsRetentionSeconds);
        _unitOfWork = unitOfWork;
        _updates = updates;
        _limits = limits;
        _log = log;
    }

    public async ValueTask<bool> EnsureAsync(long recipientAuthKeyId,
        long recipientUserId, long peerAuthKeyId, long peerUserId, int chatId,
        int date, SecretChatControlKind kind, TLEncryptedChat chat,
        CancellationToken cancellationToken = default)
    {
        byte[] updateBytes;
        try
        {
            using TLUpdate update = UpdateEncryption.Builder()
                .Chat(chat.AsSpan())
                .Date(date)
                .Build();
            updateBytes = update.AsSpan().ToArray();
        }
        finally
        {
            chat.Dispose();
        }

        long updateId = CreateUpdateId(chatId, kind);
        SemaphoreSlim gate = GetGate(recipientAuthKeyId, updateId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            IReadOnlyList<TLDto.TLSecretChatControlUpdate> existing =
                await _secretChatsRepository.GetControlUpdatesAsync(
                    recipientAuthKeyId, cancellationToken);
            bool wasAlreadyDurable = false;
            foreach (TLDto.TLSecretChatControlUpdate existingValue in existing)
            {
                using (existingValue)
                {
                    if (existingValue.AsSecretChatControlUpdate().UpdateId == updateId)
                    {
                        wasAlreadyDurable = true;
                    }
                }
            }

            using TLDto.TLSecretChatControlUpdate control =
                TLDto.SecretChatControlUpdate.Builder()
                    .RecipientAuthKeyId(recipientAuthKeyId)
                    .UpdateId(updateId)
                    .ChatId(chatId)
                    .PeerAuthKeyId(peerAuthKeyId)
                    .PeerUserId(peerUserId)
                    .Date(date)
                    .ExpiresAt(checked(date + _limits.QtsRetentionSeconds))
                    .Update(updateBytes)
                    .Build();
            if (!await _secretChatsRepository.PutControlUpdateAsync(
                    control, cancellationToken))
            {
                _log.Warning($"Secret-chat control update {updateId} for chat " +
                    $"{chatId} could not be persisted for auth key " +
                    $"{recipientAuthKeyId}.");
                return false;
            }

            if (wasAlreadyDurable)
            {
                return true;
            }

            try
            {
                await _updates.EnqueueUpdate(recipientUserId,
                    new TLUpdate(updateBytes, 0, updateBytes.Length),
                    UpdateDeliveryScope.ForAuthKey(recipientAuthKeyId));
            }
            catch (Exception exception)
            {
                _log.Warning(exception,
                    $"Secret-chat control live delivery failed for chat {chatId}, " +
                    $"auth key {recipientAuthKeyId}; durable recovery remains " +
                    "available.");
            }
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    internal static long CreateUpdateId(int chatId, SecretChatControlKind kind)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chatId);
        long value = ((long)(uint)chatId << 8) | (byte)kind;
        return -value;
    }

    public async ValueTask DeliverPersistedAsync(long recipientAuthKeyId,
        long recipientUserId, int chatId, TLUpdate update)
    {
        try
        {
            await _updates.EnqueueUpdate(recipientUserId, update,
                UpdateDeliveryScope.ForAuthKey(recipientAuthKeyId));
        }
        catch (Exception exception)
        {
            _log.Warning(exception,
                $"Secret-chat persisted control live delivery failed for chat " +
                $"{chatId}, auth key {recipientAuthKeyId}; durable recovery " +
                "remains available.");
        }
    }

    private static SemaphoreSlim[] CreateGates() =>
        Enumerable.Range(0, OrderingStripeCount)
            .Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    private SemaphoreSlim GetGate(long authKeyId, long updateId)
    {
        ulong key = unchecked((ulong)HashCode.Combine(authKeyId, updateId));
        return _orderingGates[(int)(key % (uint)_orderingGates.Length)];
    }
}
