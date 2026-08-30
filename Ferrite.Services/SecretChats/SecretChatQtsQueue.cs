// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.SecretChats;

public sealed class SecretChatQtsQueue : ISecretChatQtsQueue
{
    private readonly ISecretChatsRepository _secretChatsRepository;

    private const int OrderingStripeCount = 256;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly IUpdatesService _updates;
    private readonly SecretChatLimits _limits;
    private readonly ILogger _log;
    private readonly SecretChatTelemetry? _telemetry;
    private readonly SemaphoreSlim[] _orderingGates = CreateGates();

    public SecretChatQtsQueue(IUnitOfWork unitOfWork, ISecretChatsRepository secretChatsRepository,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        SecretChatLimits limits, ILogger log,
        SecretChatTelemetry? telemetry = null)
    {
        _secretChatsRepository = secretChatsRepository;

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            limits.MaxQtsEventsPerAuthKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            limits.MaxQtsBytesPerAuthKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limits.QtsRetentionSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            limits.DedupRetentionSeconds);
        _unitOfWork = unitOfWork;
        _updatesContextFactory = updatesContextFactory;
        _updates = updates;
        _limits = limits;
        _log = log;
        _telemetry = telemetry;
    }

    public async ValueTask<SecretChatQtsAppendResult> EnqueueAsync(
        long recipientAuthKeyId, long recipientUserId, int chatId, long randomId,
        int date, TLEncryptedMessage message,
        CancellationToken cancellationToken = default)
    {
        byte[] messageBytes;
        try
        {
            messageBytes = message.AsSpan().ToArray();
        }
        finally
        {
            message.Dispose();
        }

        cancellationToken.ThrowIfCancellationRequested();
        int expiresAt = checked(date + _limits.QtsRetentionSeconds);
        SemaphoreSlim gate = GetGate(chatId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            IUpdatesContext updatesContext = _updatesContextFactory
                .GetUpdatesContext(recipientAuthKeyId, recipientUserId);
            SecretChatQtsAppendResult result = await _secretChatsRepository.AppendQtsAsync(recipientAuthKeyId, chatId,
                    randomId, date, expiresAt, messageBytes,
                    _limits.MaxQtsEventsPerAuthKey,
                    _limits.MaxQtsBytesPerAuthKey, updatesContext.Qts,
                    updatesContext.IncrementQts, cancellationToken);
            if (result.Status != SecretChatQtsAppendStatus.Appended ||
                result.Entry is null)
            {
                _telemetry?.Rejection("qts_append", recipientAuthKeyId, chatId,
                    result.Status.ToString());
                return result;
            }

            TLDto.SecretChatQtsEntry entry =
                result.Entry.Value.AsSecretChatQtsEntry();
            int qts = entry.Qts;
            _telemetry?.QtsAppend(recipientAuthKeyId, chatId, qts,
                messageBytes.LongLength);
            TLUpdate update = UpdateNewEncryptedMessage.Builder()
                .Message(messageBytes)
                .Qts(qts)
                .Build();
            try
            {
                await _updates.EnqueueUpdate(recipientUserId, update,
                    UpdateDeliveryScope.ForAuthKey(recipientAuthKeyId));
            }
            catch (Exception exception)
            {
                _log.Warning(exception,
                    $"Secret-chat live delivery failed for chat {chatId}, " +
                    $"auth key {recipientAuthKeyId}, qts {qts}; durable recovery " +
                    "remains available.");
            }
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<SecretChatSendAppendResult> EnqueueSendAsync(
        long senderAuthKeyId, long recipientAuthKeyId, long recipientUserId,
        int chatId, long accessHash, long randomId, int date,
        TLEncryptedMessage message, ReadOnlyMemory<byte> result,
        CancellationToken cancellationToken = default)
    {
        byte[] messageBytes;
        try
        {
            messageBytes = message.AsSpan().ToArray();
        }
        finally
        {
            message.Dispose();
        }

        cancellationToken.ThrowIfCancellationRequested();
        int expiresAt = checked(date + _limits.QtsRetentionSeconds);
        SemaphoreSlim gate = GetGate(chatId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            IUpdatesContext updatesContext = _updatesContextFactory
                .GetUpdatesContext(recipientAuthKeyId, recipientUserId);
            SecretChatSendAppendResult append = await _secretChatsRepository.AppendSendQtsAsync(senderAuthKeyId,
                    recipientAuthKeyId, chatId, accessHash, randomId, date,
                    expiresAt, messageBytes, result,
                    _limits.MaxQtsEventsPerAuthKey,
                    _limits.MaxQtsBytesPerAuthKey,
                    _limits.DedupRetentionSeconds, updatesContext.Qts,
                    updatesContext.IncrementQts, cancellationToken);
            if (append.Status != SecretChatSendAppendStatus.Appended ||
                append.Entry is null)
            {
                _telemetry?.Rejection("send_append", senderAuthKeyId, chatId,
                    append.Status.ToString());
                return append;
            }

            int qts = append.Entry.Value.AsSecretChatQtsEntry().Qts;
            _telemetry?.QtsAppend(recipientAuthKeyId, chatId, qts,
                messageBytes.LongLength);
            TLUpdate update = UpdateNewEncryptedMessage.Builder()
                .Message(messageBytes)
                .Qts(qts)
                .Build();
            try
            {
                await _updates.EnqueueUpdate(recipientUserId, update,
                    UpdateDeliveryScope.ForAuthKey(recipientAuthKeyId));
            }
            catch (Exception exception)
            {
                _log.Warning(exception,
                    $"Secret-chat live delivery failed for chat {chatId}, " +
                    $"auth key {recipientAuthKeyId}, qts {qts}; durable recovery " +
                    "remains available.");
            }
            return append;
        }
        finally
        {
            gate.Release();
        }
    }

    private static SemaphoreSlim[] CreateGates() =>
        Enumerable.Range(0, OrderingStripeCount)
            .Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    private SemaphoreSlim GetGate(int chatId)
    {
        ulong key = unchecked((ulong)chatId);
        return _orderingGates[(int)(key % (uint)_orderingGates.Length)];
    }
}
