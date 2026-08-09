// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Security.Cryptography;
using Ferrite.TL;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class SecretChatsRepository : ISecretChatsRepository
{
    private const int StripeCount = 256;
    private readonly IKVStore _chats;
    private readonly IKVStore _requestedDevices;
    private readonly IKVStore _pendingPairs;
    private readonly IKVStore _qtsEntries;
    private readonly IKVStore _qtsStates;
    private readonly IKVStore _sendReceipts;
    private readonly IKVStore _encryptedFiles;
    private readonly IKVStore _encryptedFileAssociations;
    private readonly IKVStore _controlUpdates;
    private readonly IKVStore _controlTargets;
    private readonly IKVStore _revocations;
    private readonly Func<ValueTask<bool>> _flush;
    private readonly SemaphoreSlim[] _chatGates = CreateGates();
    private readonly SemaphoreSlim[] _authKeyGates = CreateGates();
    private readonly SemaphoreSlim[] _fileGates = CreateGates();

    public SecretChatsRepository(IKVStore chats, IKVStore requestedDevices,
        IKVStore pendingPairs, IKVStore qtsEntries, IKVStore qtsStates,
        IKVStore sendReceipts,
        IKVStore encryptedFiles, IKVStore encryptedFileAssociations,
        IKVStore controlUpdates, IKVStore controlTargets, IKVStore revocations,
        Func<ValueTask<bool>>? flush = null)
    {
        _chats = chats;
        chats.SetSchema(new TableDefinition("ferrite", "secret_chats",
            new KeyDefinition("pk",
                new DataColumn { Name = "chat_id", Type = DataType.Int },
                new DataColumn { Name = "initiator_auth_key_id", Type = DataType.Long },
                new DataColumn { Name = "recipient_auth_key_id", Type = DataType.Long },
                new DataColumn { Name = "initiator_user_id", Type = DataType.Long },
                new DataColumn { Name = "random_id", Type = DataType.Int }),
            new KeyDefinition("by_initiator_auth_key",
                new DataColumn { Name = "initiator_auth_key_id", Type = DataType.Long },
                new DataColumn { Name = "chat_id", Type = DataType.Int }),
            new KeyDefinition("by_recipient_auth_key",
                new DataColumn { Name = "recipient_auth_key_id", Type = DataType.Long },
                new DataColumn { Name = "chat_id", Type = DataType.Int }),
            new KeyDefinition("by_initiator_random_id",
                new DataColumn { Name = "initiator_user_id", Type = DataType.Long },
                new DataColumn { Name = "random_id", Type = DataType.Int }),
            new KeyDefinition("by_initiator_user",
                new DataColumn { Name = "initiator_user_id", Type = DataType.Long },
                new DataColumn { Name = "chat_id", Type = DataType.Int })));

        _requestedDevices = requestedDevices;
        requestedDevices.SetSchema(new TableDefinition("ferrite",
            "secret_chat_requested_devices",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long },
                new DataColumn { Name = "chat_id", Type = DataType.Int })));

        _pendingPairs = pendingPairs;
        pendingPairs.SetSchema(new TableDefinition("ferrite",
            "secret_chat_pending_pairs",
            new KeyDefinition("pk",
                new DataColumn { Name = "initiator_user_id", Type = DataType.Long },
                new DataColumn { Name = "recipient_user_id", Type = DataType.Long })));

        _qtsEntries = qtsEntries;
        qtsEntries.SetSchema(new TableDefinition("ferrite", "secret_chat_qts_entries",
            new KeyDefinition("pk",
                new DataColumn { Name = "recipient_auth_key_id", Type = DataType.Long },
                new DataColumn { Name = "qts", Type = DataType.Int })));

        _qtsStates = qtsStates;
        qtsStates.SetSchema(new TableDefinition("ferrite", "secret_chat_qts_state",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long })));

        _sendReceipts = sendReceipts;
        sendReceipts.SetSchema(new TableDefinition("ferrite", "secret_chat_send_receipts",
            new KeyDefinition("pk",
                new DataColumn { Name = "chat_id", Type = DataType.Int },
                new DataColumn { Name = "sender_auth_key_id", Type = DataType.Long },
                new DataColumn { Name = "random_id", Type = DataType.Long })));

        _encryptedFiles = encryptedFiles;
        encryptedFiles.SetSchema(new TableDefinition("ferrite", "secret_chat_files",
            new KeyDefinition("pk",
                new DataColumn { Name = "file_id", Type = DataType.Long },
                new DataColumn { Name = "access_hash", Type = DataType.Long },
                new DataColumn { Name = "upload_file_id", Type = DataType.Long }),
            new KeyDefinition("by_exact",
                new DataColumn { Name = "file_id", Type = DataType.Long },
                new DataColumn { Name = "access_hash", Type = DataType.Long }),
            new KeyDefinition("by_id",
                new DataColumn { Name = "file_id", Type = DataType.Long }),
            new KeyDefinition("by_upload",
                new DataColumn { Name = "upload_file_id", Type = DataType.Long })));

        _encryptedFileAssociations = encryptedFileAssociations;
        encryptedFileAssociations.SetSchema(new TableDefinition("ferrite",
            "secret_chat_file_associations",
            new KeyDefinition("pk",
                new DataColumn { Name = "file_id", Type = DataType.Long },
                new DataColumn { Name = "chat_id", Type = DataType.Int }),
            new KeyDefinition("by_chat",
                new DataColumn { Name = "chat_id", Type = DataType.Int },
                new DataColumn { Name = "file_id", Type = DataType.Long })));

        _controlUpdates = controlUpdates;
        controlUpdates.SetSchema(new TableDefinition("ferrite",
            "secret_chat_control_updates",
            new KeyDefinition("pk",
                new DataColumn { Name = "recipient_auth_key_id", Type = DataType.Long },
                new DataColumn { Name = "update_id", Type = DataType.Long })));

        _controlTargets = controlTargets;
        controlTargets.SetSchema(new TableDefinition("ferrite",
            "secret_chat_control_targets",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long },
                new DataColumn { Name = "chat_id", Type = DataType.Int }),
            new KeyDefinition("by_chat",
                new DataColumn { Name = "chat_id", Type = DataType.Int },
                new DataColumn { Name = "auth_key_id", Type = DataType.Long })));

        _revocations = revocations;
        _flush = flush ?? (() => ValueTask.FromResult(true));
        revocations.SetSchema(new TableDefinition("ferrite",
            "secret_chat_auth_key_revocations",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long })));
    }

    public async ValueTask<SecretChatCreateResult> TryCreateChatAsync(
        TLDto.TLSecretChatState chat, int maxPendingChatsPerAuthKey = int.MaxValue,
        int maxOutstandingRequestsPerAuthKey = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        int chatId = ChatId(chat);
        long initiatorAuthKeyId = InitiatorAuthKeyId(chat);
        long initiatorUserId = InitiatorUserId(chat);
        long recipientUserId = RecipientUserId(chat);
        long[] requestedAuthKeys = RequestedRecipientAuthKeyIds(chat).Distinct()
            .ToArray();
        IReadOnlyList<SemaphoreSlim> authGates = await AcquireAuthKeyGatesAsync(
            requestedAuthKeys.Append(initiatorAuthKeyId), cancellationToken);
        SemaphoreSlim chatGate = GetGate(_chatGates, chatId);
        await chatGate.WaitAsync(cancellationToken);
        try
        {
            if (await IsRevokedAsync(initiatorAuthKeyId))
            {
                return new SecretChatCreateResult(SecretChatCreateStatus.AuthKeyRevoked, null);
            }

            TLDto.TLSecretChatState? byId = await GetChatInternalAsync(chatId,
                cancellationToken);
            if (byId is not null)
            {
                bool idempotent = IsIdempotentCreate(byId.Value, chat);
                return new SecretChatCreateResult(idempotent
                    ? SecretChatCreateStatus.Idempotent
                    : SecretChatCreateStatus.ChatIdCollision, byId.Value);
            }

            TLDto.TLSecretChatState? byRandom =
                await GetChatByInitiatorRandomIdInternalAsync(
                    initiatorUserId, chatId, cancellationToken);
            if (byRandom is not null)
            {
                return new SecretChatCreateResult(
                    SecretChatCreateStatus.InitiatorRandomIdCollision, byRandom.Value);
            }

            TLDto.TLSecretChatPendingPair? pendingPair = await GetPendingPairAsync(
                initiatorUserId, recipientUserId);
            if (pendingPair is not null)
            {
                int pendingChatId;
                using (TLDto.TLSecretChatPendingPair ownedPair = pendingPair.Value)
                {
                    pendingChatId = ownedPair.AsSecretChatPendingPair().ChatId;
                }
                TLDto.TLSecretChatState? pendingChat = await GetChatInternalAsync(
                    pendingChatId, cancellationToken);
                if (pendingChat is not null)
                {
                    TLDto.TLSecretChatState ownedPendingChat = pendingChat.Value;
                    if (State(ownedPendingChat) == SecretChatPersistenceState.Pending)
                    {
                        return new SecretChatCreateResult(
                            SecretChatCreateStatus.PairRequestExists,
                            ownedPendingChat);
                    }
                }
                _pendingPairs.Delete(initiatorUserId, recipientUserId);
            }

            TLDto.TLSecretChatState? indexedPending =
                await GetPendingChatForPairAsync(initiatorUserId, recipientUserId,
                    cancellationToken);
            if (indexedPending is not null)
            {
                TLDto.TLSecretChatState ownedPending = indexedPending.Value;
                using TLDto.TLSecretChatPendingPair repairedPair =
                    BuildPendingPair(ownedPending);
                _pendingPairs.Put(repairedPair.AsSpan().ToArray(), initiatorUserId,
                    recipientUserId);
                await FlushAsync("secret-chat pending-pair repair");
                return new SecretChatCreateResult(
                    SecretChatCreateStatus.PairRequestExists, ownedPending);
            }

            if (await CountChatsByStateAsync(initiatorAuthKeyId,
                    SecretChatPersistenceState.Pending, cancellationToken) >=
                Math.Max(0, maxPendingChatsPerAuthKey))
            {
                return new SecretChatCreateResult(
                    SecretChatCreateStatus.PendingLimitExceeded, null);
            }

            List<long> eligibleKeys = new();
            foreach (long authKeyId in requestedAuthKeys)
            {
                if (!await IsRevokedAsync(authKeyId))
                {
                    eligibleKeys.Add(authKeyId);
                }
            }
            if (eligibleKeys.Count == 0)
            {
                return new SecretChatCreateResult(
                    SecretChatCreateStatus.RecipientUnavailable, null);
            }
            foreach (long authKeyId in eligibleKeys)
            {
                if (await CountRequestedChatsAsync(authKeyId, cancellationToken) >=
                    Math.Max(0, maxOutstandingRequestsPerAuthKey))
                {
                    return new SecretChatCreateResult(
                        SecretChatCreateStatus.RecipientRequestLimitExceeded, null);
                }
            }

            TLDto.TLSecretChatState stored = BuildPendingChat(chat, eligibleKeys);
            try
            {
                PutChat(stored);
                using (TLDto.TLSecretChatPendingPair pair = BuildPendingPair(stored))
                {
                    _pendingPairs.Put(pair.AsSpan().ToArray(), initiatorUserId,
                        recipientUserId);
                }
                foreach (long authKeyId in eligibleKeys)
                {
                    using TLDto.TLSecretChatRequestedDevice target =
                        BuildRequestedDevice(authKeyId, ChatId(stored),
                            RecipientUserId(stored), CreatedAt(stored));
                    _requestedDevices.Put(target.AsSpan().ToArray(), authKeyId,
                        ChatId(stored));
                    using TLDto.TLSecretChatControlTarget controlTarget =
                        BuildControlTarget(authKeyId, ChatId(stored),
                            RecipientUserId(stored), CreatedAt(stored));
                    _controlTargets.Put(controlTarget.AsSpan().ToArray(), authKeyId,
                        ChatId(stored));
                }
                await FlushAsync("secret-chat request");
            }
            catch
            {
                stored.Dispose();
                throw;
            }

            return new SecretChatCreateResult(SecretChatCreateStatus.Created, stored);
        }
        finally
        {
            chatGate.Release();
            ReleaseGates(authGates);
        }
    }

    public ValueTask<TLDto.TLSecretChatState?> GetChatAsync(int chatId,
        CancellationToken cancellationToken = default) =>
        GetChatInternalAsync(chatId, cancellationToken);

    public ValueTask<TLDto.TLSecretChatState?> GetChatByInitiatorRandomIdAsync(
        long initiatorUserId, int randomId, CancellationToken cancellationToken = default) =>
        GetChatByInitiatorRandomIdInternalAsync(initiatorUserId, randomId,
            cancellationToken);

    public async ValueTask<IReadOnlyList<TLDto.TLSecretChatState>> GetChatsByAuthKeyAsync(
        long authKeyId, CancellationToken cancellationToken = default)
    {
        Dictionary<int, TLDto.TLSecretChatState> chats = new();
        await foreach (byte[] bytes in _chats.IterateBySecondaryIndexAsync(
                           "by_initiator_auth_key", authKeyId)
                           .WithCancellation(cancellationToken))
        {
            TLDto.TLSecretChatState row = ReadChat(bytes);
            chats[ChatId(row)] = row;
        }
        await foreach (byte[] bytes in _chats.IterateBySecondaryIndexAsync(
                           "by_recipient_auth_key", authKeyId)
                           .WithCancellation(cancellationToken))
        {
            TLDto.TLSecretChatState row = ReadChat(bytes);
            chats[ChatId(row)] = row;
        }
        return chats.Values.OrderBy(ChatId).ToArray();
    }

    public async ValueTask<IReadOnlyList<long>> GetRequestedRecipientAuthKeysAsync(
        int chatId, CancellationToken cancellationToken = default)
    {
        TLDto.TLSecretChatState? chat = await GetChatInternalAsync(chatId,
            cancellationToken);
        return chat is null ? [] : RequestedRecipientAuthKeyIds(chat.Value);
    }

    public async ValueTask<SecretChatAcceptResult> TryAcceptChatAsync(int chatId,
        long recipientAuthKeyId, ReadOnlyMemory<byte> gB, long keyFingerprint, int date,
        int maxActiveChatsPerAuthKey = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        byte[] gBCopy = gB.ToArray();
        TLDto.TLSecretChatState? initial = await GetChatInternalAsync(chatId,
            cancellationToken);
        if (initial is null)
        {
            return new SecretChatAcceptResult(SecretChatAcceptStatus.NotFound, null, []);
        }
        long[] transitionAuthKeyIds;
        using (TLDto.TLSecretChatState initialChat = initial.Value)
        {
            transitionAuthKeyIds = RequestedRecipientAuthKeyIds(initialChat)
                .Append(InitiatorAuthKeyId(initialChat))
                .Append(recipientAuthKeyId)
                .Distinct()
                .ToArray();
        }
        IReadOnlyList<SemaphoreSlim> authGates = await AcquireAuthKeyGatesAsync(
            transitionAuthKeyIds, cancellationToken);
        SemaphoreSlim chatGate = GetGate(_chatGates, chatId);
        await chatGate.WaitAsync(cancellationToken);
        try
        {
            TLDto.TLSecretChatState? existing = await GetChatInternalAsync(chatId,
                cancellationToken);
            if (existing is null)
            {
                return new SecretChatAcceptResult(SecretChatAcceptStatus.NotFound, null, []);
            }
            TLDto.TLSecretChatState existingChat = existing.Value;
            if (State(existingChat) != SecretChatPersistenceState.Pending)
            {
                return new SecretChatAcceptResult(SecretChatAcceptStatus.NotPending,
                    existingChat, []);
            }
            long[] requestedAuthKeys = RequestedRecipientAuthKeyIds(existingChat);
            if (!requestedAuthKeys.Contains(recipientAuthKeyId))
            {
                return new SecretChatAcceptResult(
                    SecretChatAcceptStatus.RecipientNotRequested, existingChat, []);
            }
            if (await IsRevokedAsync(recipientAuthKeyId))
            {
                return new SecretChatAcceptResult(SecretChatAcceptStatus.AuthKeyRevoked,
                    existingChat, []);
            }

            long initiatorAuthKeyId = InitiatorAuthKeyId(existingChat);
            if (await IsRevokedAsync(initiatorAuthKeyId))
            {
                return new SecretChatAcceptResult(SecretChatAcceptStatus.AuthKeyRevoked,
                    existingChat, []);
            }
            int activeLimit = Math.Max(0, maxActiveChatsPerAuthKey);
            if (await CountChatsByStateAsync(initiatorAuthKeyId,
                    SecretChatPersistenceState.Active, cancellationToken) >= activeLimit ||
                await CountChatsByStateAsync(recipientAuthKeyId,
                    SecretChatPersistenceState.Active, cancellationToken) >= activeLimit)
            {
                return new SecretChatAcceptResult(
                    SecretChatAcceptStatus.ActiveLimitExceeded, existingChat, []);
            }

            long[] losingKeys = requestedAuthKeys
                .Where(x => x != recipientAuthKeyId).Distinct().ToArray();
            TLDto.TLSecretChatState accepted = BuildAcceptedChat(existingChat,
                recipientAuthKeyId, gBCopy, keyFingerprint, date);
            try
            {
                ReplaceChat(existingChat, accepted);
                await DeleteControlTargetsByChatAsync(chatId, cancellationToken);
                DeleteRequestedDevices(existingChat);
                using (TLDto.TLSecretChatControlTarget initiatorTarget =
                       BuildControlTarget(initiatorAuthKeyId, chatId,
                           InitiatorUserId(existingChat), date))
                {
                    _controlTargets.Put(initiatorTarget.AsSpan().ToArray(),
                        initiatorAuthKeyId, chatId);
                }
                foreach (long losingKey in losingKeys)
                {
                    using TLDto.TLSecretChatControlTarget losingTarget =
                        BuildControlTarget(losingKey, chatId,
                            RecipientUserId(existingChat), date);
                    _controlTargets.Put(losingTarget.AsSpan().ToArray(), losingKey,
                        chatId);
                }
                await FlushAsync("secret-chat acceptance");
            }
            catch
            {
                accepted.Dispose();
                throw;
            }
            return new SecretChatAcceptResult(SecretChatAcceptStatus.Accepted, accepted,
                losingKeys);
        }
        finally
        {
            chatGate.Release();
            ReleaseGates(authGates);
        }
    }

    public async ValueTask<SecretChatDiscardResult> TryDiscardChatAsync(int chatId,
        long callerAuthKeyId, bool deleteHistory, int date,
        CancellationToken cancellationToken = default)
    {
        TLDto.TLSecretChatState? initial = await GetChatInternalAsync(chatId,
            cancellationToken);
        if (initial is null)
        {
            return new SecretChatDiscardResult(SecretChatDiscardStatus.NotFound, null,
                []);
        }
        long[] transitionAuthKeyIds;
        SecretChatPersistenceState initialState;
        using (TLDto.TLSecretChatState initialChat = initial.Value)
        {
            TLDto.SecretChatState initialRow = initialChat.AsSecretChatState();
            initialState = State(initialChat);
            transitionAuthKeyIds = RequestedRecipientAuthKeyIds(initialChat)
                .Append(initialRow.InitiatorAuthKeyId)
                .Append(initialRow.Flags[1] ? initialRow.RecipientAuthKeyId : 0L)
                .Append(callerAuthKeyId)
                .Where(x => x != 0)
                .Distinct()
                .ToArray();
        }
        IReadOnlyList<SemaphoreSlim> authGates = await AcquireAuthKeyGatesAsync(
            transitionAuthKeyIds, cancellationToken);
        SemaphoreSlim chatGate = GetGate(_chatGates, chatId);
        await chatGate.WaitAsync(cancellationToken);
        try
        {
            TLDto.TLSecretChatState? existing = await GetChatInternalAsync(chatId,
                cancellationToken);
            if (existing is null)
            {
                return new SecretChatDiscardResult(SecretChatDiscardStatus.NotFound, null,
                    []);
            }
            TLDto.TLSecretChatState existingChat = existing.Value;
            TLDto.SecretChatState row = existingChat.AsSecretChatState();
            long[] requestedAuthKeys = RequestedRecipientAuthKeyIds(existingChat);
            long? recipientAuthKeyId = row.Flags[1] ? row.RecipientAuthKeyId : null;
            SecretChatPersistenceState state = State(existingChat);
            bool isInitiator = row.InitiatorAuthKeyId == callerAuthKeyId;
            bool isRequestedRecipient = requestedAuthKeys.Contains(callerAuthKeyId);
            bool isBoundRecipient = recipientAuthKeyId == callerAuthKeyId;
            if (state == SecretChatPersistenceState.Discarded)
            {
                SecretChatDiscardStatus terminalStatus;
                if (recipientAuthKeyId is not null && isRequestedRecipient &&
                    !isBoundRecipient && !isInitiator)
                {
                    terminalStatus = SecretChatDiscardStatus.AlreadyAccepted;
                }
                else if (isInitiator || isBoundRecipient ||
                         recipientAuthKeyId is null && isRequestedRecipient)
                {
                    terminalStatus = SecretChatDiscardStatus.AlreadyDiscarded;
                }
                else
                {
                    terminalStatus = SecretChatDiscardStatus.Unauthorized;
                }
                return new SecretChatDiscardResult(terminalStatus, existingChat, []);
            }
            if (initialState == SecretChatPersistenceState.Pending &&
                state == SecretChatPersistenceState.Active)
            {
                return new SecretChatDiscardResult(
                    SecretChatDiscardStatus.AlreadyAccepted, existingChat, []);
            }
            if (state == SecretChatPersistenceState.Active && isRequestedRecipient &&
                !isBoundRecipient && !isInitiator)
            {
                return new SecretChatDiscardResult(
                    SecretChatDiscardStatus.AlreadyAccepted, existingChat, []);
            }
            if ((state == SecretChatPersistenceState.Pending &&
                 !isInitiator && !isRequestedRecipient) ||
                (state == SecretChatPersistenceState.Active &&
                 !isInitiator && !isBoundRecipient))
            {
                return new SecretChatDiscardResult(
                    SecretChatDiscardStatus.Unauthorized, existingChat, []);
            }

            long[] notificationAuthKeyIds = state == SecretChatPersistenceState.Pending
                ? requestedAuthKeys.Append(row.InitiatorAuthKeyId)
                    .Where(x => x != callerAuthKeyId).Distinct().ToArray()
                : new long[] { row.InitiatorAuthKeyId, recipientAuthKeyId!.Value }
                    .Where(x => x != callerAuthKeyId).Distinct().ToArray();
            long initiatorAuthKeyId = row.InitiatorAuthKeyId;
            long initiatorUserId = row.InitiatorUserId;
            long recipientUserId = row.RecipientUserId;

            TLDto.TLSecretChatState discarded = BuildDiscardedChat(existingChat,
                deleteHistory, date);
            try
            {
                ReplaceChat(existingChat, discarded);
                await DeleteControlTargetsByChatAsync(chatId, cancellationToken);
                DeleteRequestedDevices(existingChat);
                foreach (long notificationAuthKeyId in notificationAuthKeyIds)
                {
                    long notificationUserId = notificationAuthKeyId ==
                        initiatorAuthKeyId
                            ? initiatorUserId
                            : recipientUserId;
                    using TLDto.TLSecretChatControlTarget target = BuildControlTarget(
                        notificationAuthKeyId, chatId, notificationUserId, date);
                    _controlTargets.Put(target.AsSpan().ToArray(),
                        notificationAuthKeyId, chatId);
                }
                await FlushAsync("secret-chat discard");
            }
            catch
            {
                discarded.Dispose();
                throw;
            }
            return new SecretChatDiscardResult(SecretChatDiscardStatus.Discarded,
                discarded, notificationAuthKeyIds);
        }
        finally
        {
            chatGate.Release();
            ReleaseGates(authGates);
        }
    }

    public async ValueTask<IReadOnlyList<TLDto.TLSecretChatState>>
        GetControlTargetChatsAsync(long authKeyId,
            CancellationToken cancellationToken = default)
    {
        var chats = new Dictionary<int, TLDto.TLSecretChatState>();
        await foreach (byte[] bytes in _controlTargets.IterateAsync(authKeyId)
                           .WithCancellation(cancellationToken))
        {
            using TLDto.TLSecretChatControlTarget target =
                ReadControlTarget(bytes);
            int chatId = target.AsSecretChatControlTarget().ChatId;
            TLDto.TLSecretChatState? chat = await GetChatInternalAsync(chatId,
                cancellationToken);
            if (chat is not null)
            {
                chats[chatId] = chat.Value;
            }
        }
        return chats.Values.OrderBy(ChatId).ToArray();
    }

    public async ValueTask<IReadOnlyList<long>> GetControlTargetAuthKeyIdsAsync(
        int chatId, CancellationToken cancellationToken = default)
    {
        var authKeyIds = new HashSet<long>();
        await foreach (byte[] bytes in _controlTargets
                           .IterateBySecondaryIndexAsync("by_chat", chatId)
                           .WithCancellation(cancellationToken))
        {
            using TLDto.TLSecretChatControlTarget target =
                ReadControlTarget(bytes);
            authKeyIds.Add(target.AsSecretChatControlTarget().AuthKeyId);
        }
        return authKeyIds.Order().ToArray();
    }

    public async ValueTask<bool> CompleteControlTransitionAsync(int chatId,
        SecretChatPersistenceState expectedState, int expectedDate,
        CancellationToken cancellationToken = default)
    {
        SemaphoreSlim chatGate = GetGate(_chatGates, chatId);
        await chatGate.WaitAsync(cancellationToken);
        try
        {
            TLDto.TLSecretChatState? chat = await GetChatInternalAsync(chatId,
                cancellationToken);
            if (chat is null)
            {
                return false;
            }
            using TLDto.TLSecretChatState ownedChat = chat.Value;
            TLDto.SecretChatState row = ownedChat.AsSecretChatState();
            if ((SecretChatPersistenceState)row.State != expectedState ||
                row.UpdatedAt != expectedDate)
            {
                return false;
            }
            await DeleteControlTargetsByChatAsync(chatId, cancellationToken);
            await FlushAsync("secret-chat control-transition completion");
            return true;
        }
        finally
        {
            chatGate.Release();
        }
    }

    public async ValueTask<SecretChatReceiptPutResult> TryPutSendReceiptAsync(
        TLDto.TLSecretChatSendReceipt receipt,
        CancellationToken cancellationToken = default)
    {
        int chatId = receipt.AsSecretChatSendReceipt().ChatId;
        long senderAuthKeyId = receipt.AsSecretChatSendReceipt().SenderAuthKeyId;
        long randomId = receipt.AsSecretChatSendReceipt().RandomId;
        SemaphoreSlim gate = GetGate(_chatGates, chatId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            TLDto.TLSecretChatSendReceipt? existing = await GetSendReceiptAsync(chatId,
                senderAuthKeyId, randomId, cancellationToken: cancellationToken);
            if (existing is not null)
            {
                return new SecretChatReceiptPutResult(
                    SecretChatReceiptPutStatus.AlreadyExists, existing.Value);
            }
            TLDto.TLSecretChatSendReceipt stored =
                receipt.AsSecretChatSendReceipt().Clone().Build();
            try
            {
                _sendReceipts.Put(stored.AsSpan().ToArray(), chatId, senderAuthKeyId,
                    randomId);
                await FlushAsync("secret-chat send receipt");
            }
            catch
            {
                stored.Dispose();
                throw;
            }
            return new SecretChatReceiptPutResult(SecretChatReceiptPutStatus.Created,
                stored);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<TLDto.TLSecretChatSendReceipt?> GetSendReceiptAsync(int chatId,
        long senderAuthKeyId, long randomId,
        int minimumDate = int.MinValue,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? bytes = await _sendReceipts.GetAsync(chatId, senderAuthKeyId, randomId);
        if (bytes is not { Length: > 0 })
        {
            return null;
        }
        TLDto.TLSecretChatSendReceipt receipt = ReadSendReceipt(bytes);
        if (receipt.AsSecretChatSendReceipt().Date >= minimumDate)
        {
            return receipt;
        }
        receipt.Dispose();
        return null;
    }

    public async ValueTask<SecretChatReadAdvanceStatus> AdvanceReadDateAsync(
        long callerAuthKeyId, int chatId, long accessHash, int maxDate,
        TLDto.TLSecretChatControlUpdate controlUpdate,
        CancellationToken cancellationToken = default)
    {
        TLDto.SecretChatControlUpdate control =
            controlUpdate.AsSecretChatControlUpdate();
        long recipientAuthKeyId = control.RecipientAuthKeyId;
        long controlUpdateId = control.UpdateId;
        int controlChatId = control.ChatId;
        long controlPeerAuthKeyId = control.PeerAuthKeyId;
        long controlPeerUserId = control.PeerUserId;
        byte[] controlBytes = controlUpdate.AsSpan().ToArray();
        IReadOnlyList<SemaphoreSlim> authGates = await AcquireAuthKeyGatesAsync(
            new[] { callerAuthKeyId, recipientAuthKeyId }, cancellationToken);
        SemaphoreSlim chatGate = GetGate(_chatGates, chatId);
        await chatGate.WaitAsync(cancellationToken);
        try
        {
            if (await IsRevokedAsync(callerAuthKeyId) ||
                await IsRevokedAsync(recipientAuthKeyId))
            {
                return SecretChatReadAdvanceStatus.AuthKeyRevoked;
            }
            TLDto.TLSecretChatState? chatValue = await GetChatInternalAsync(chatId,
                cancellationToken);
            if (chatValue is null)
            {
                return SecretChatReadAdvanceStatus.NotFound;
            }
            using TLDto.TLSecretChatState chat = chatValue.Value;
            TLDto.SecretChatState row = chat.AsSecretChatState();
            if (row.AccessHash != accessHash)
            {
                return SecretChatReadAdvanceStatus.AccessHashInvalid;
            }
            if ((SecretChatPersistenceState)row.State !=
                SecretChatPersistenceState.Active || !row.Flags[1])
            {
                return SecretChatReadAdvanceStatus.NotActive;
            }

            bool callerIsInitiator = callerAuthKeyId == row.InitiatorAuthKeyId;
            bool callerIsRecipient = callerAuthKeyId == row.RecipientAuthKeyId;
            long expectedRecipientAuthKeyId = callerIsInitiator
                ? row.RecipientAuthKeyId
                : row.InitiatorAuthKeyId;
            long callerUserId = callerIsInitiator
                ? row.InitiatorUserId
                : row.RecipientUserId;
            if ((!callerIsInitiator && !callerIsRecipient) ||
                recipientAuthKeyId != expectedRecipientAuthKeyId ||
                controlChatId != chatId ||
                controlPeerAuthKeyId != callerAuthKeyId ||
                controlPeerUserId != callerUserId)
            {
                return SecretChatReadAdvanceStatus.Unauthorized;
            }

            int currentMaxDate = callerIsInitiator
                ? row.InitiatorReadMaxDate
                : row.RecipientReadMaxDate;
            if (maxDate <= currentMaxDate)
            {
                return SecretChatReadAdvanceStatus.Unchanged;
            }

            using TLDto.TLSecretChatState updated = BuildReadChat(chat,
                callerIsInitiator, maxDate);
            ReplaceChat(chat, updated);
            _controlUpdates.Put(controlBytes, recipientAuthKeyId, controlUpdateId);
            await FlushAsync("secret-chat read state and control update");
            return SecretChatReadAdvanceStatus.Advanced;
        }
        finally
        {
            chatGate.Release();
            ReleaseGates(authGates);
        }
    }

    public async ValueTask<SecretChatQtsAppendResult> AppendQtsAsync(
        long recipientAuthKeyId, int chatId, long randomId, int date, int expiresAt,
        ReadOnlyMemory<byte> encryptedMessage, int maxEvents, long maxBytes,
        Func<ValueTask<int>> getCurrentQts, Func<ValueTask<int>> incrementQts,
        CancellationToken cancellationToken = default)
    {
        byte[] messageCopy = encryptedMessage.ToArray();
        SemaphoreSlim gate = GetGate(_authKeyGates, recipientAuthKeyId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (await IsRevokedAsync(recipientAuthKeyId))
            {
                return new SecretChatQtsAppendResult(
                    SecretChatQtsAppendStatus.AuthKeyRevoked, null);
            }
            TLDto.TLSecretChatQtsState state = await GetOrCreateQtsStateAsync(
                recipientAuthKeyId, cancellationToken);
            try
            {
                state = await RecoverPendingQtsAppendAsync(state, getCurrentQts,
                    incrementQts);
                ExpiredQtsResult expiry = await ExpireContiguousQtsAsync(state,
                    recipientAuthKeyId, date, cancellationToken);
                state = expiry.State;
                int lastPersistedQts = state.AsSecretChatQtsState().LastPersistedQts;
                int acknowledgedQts = state.AsSecretChatQtsState().AcknowledgedQts;
                int queuedEvents = state.AsSecretChatQtsState().QueuedEvents;
                long queuedBytes = state.AsSecretChatQtsState().QueuedBytes;
                if (queuedEvents >= maxEvents)
                {
                    return new SecretChatQtsAppendResult(
                        SecretChatQtsAppendStatus.EventLimitExceeded, null);
                }
                if (messageCopy.LongLength > maxBytes - queuedBytes)
                {
                    return new SecretChatQtsAppendResult(
                        SecretChatQtsAppendStatus.ByteLimitExceeded, null);
                }

                using TLDto.TLSecretChatQtsPending pending = BuildQtsPending(chatId,
                    randomId, date, expiresAt, messageCopy);
                using TLDto.TLSecretChatQtsState reserved = BuildQtsState(
                    recipientAuthKeyId, lastPersistedQts, acknowledgedQts, queuedEvents,
                    queuedBytes, pending);
                PutQtsState(reserved);
                await FlushAsync("secret-chat qts reservation");

                int qts = await incrementQts();
                if (qts <= lastPersistedQts)
                {
                    throw new InvalidOperationException(
                        "The canonical qts counter did not advance.");
                }
                TLDto.TLSecretChatQtsEntry entry = BuildQtsEntry(recipientAuthKeyId,
                    qts, chatId, randomId, date, expiresAt, messageCopy);
                try
                {
                    using TLDto.TLSecretChatQtsState next = BuildQtsState(
                        recipientAuthKeyId, qts, acknowledgedQts,
                        checked(queuedEvents + 1),
                        checked(queuedBytes + messageCopy.LongLength), null);
                    _qtsEntries.Put(entry.AsSpan().ToArray(), recipientAuthKeyId, qts);
                    PutQtsState(next);
                    await FlushAsync("secret-chat qts entry");
                }
                catch
                {
                    entry.Dispose();
                    throw;
                }
                return new SecretChatQtsAppendResult(SecretChatQtsAppendStatus.Appended,
                    entry);
            }
            finally
            {
                state.Dispose();
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<SecretChatSendAppendResult> AppendSendQtsAsync(
        long senderAuthKeyId, long recipientAuthKeyId, int chatId, long accessHash,
        long randomId, int date, int expiresAt,
        ReadOnlyMemory<byte> encryptedMessage, ReadOnlyMemory<byte> result,
        int maxEvents, long maxBytes, int receiptRetentionSeconds,
        Func<ValueTask<int>> getCurrentQts,
        Func<ValueTask<int>> incrementQts,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            receiptRetentionSeconds);
        byte[] messageCopy = encryptedMessage.ToArray();
        byte[] resultCopy = result.ToArray();
        IReadOnlyList<SemaphoreSlim> authGates = await AcquireAuthKeyGatesAsync(
            new[] { senderAuthKeyId, recipientAuthKeyId }, cancellationToken);
        try
        {
            if (await IsRevokedAsync(senderAuthKeyId) ||
                await IsRevokedAsync(recipientAuthKeyId))
            {
                return new SecretChatSendAppendResult(
                    SecretChatSendAppendStatus.AuthKeyRevoked, null, null);
            }

            TLDto.TLSecretChatQtsState state = await GetOrCreateQtsStateAsync(
                recipientAuthKeyId, cancellationToken);
            try
            {
                state = await RecoverPendingQtsAppendAsync(state, getCurrentQts,
                    incrementQts);
                ExpiredQtsResult expiry = await ExpireContiguousQtsAsync(state,
                    recipientAuthKeyId, date, cancellationToken);
                state = expiry.State;

                SemaphoreSlim chatGate = GetGate(_chatGates, chatId);
                await chatGate.WaitAsync(cancellationToken);
                try
                {
                    TLDto.TLSecretChatState? chatValue = await GetChatInternalAsync(
                        chatId, cancellationToken);
                    if (chatValue is null)
                    {
                        return new SecretChatSendAppendResult(
                            SecretChatSendAppendStatus.NotFound, null, null);
                    }
                    using TLDto.TLSecretChatState chat = chatValue.Value;
                    TLDto.SecretChatState chatRow = chat.AsSecretChatState();
                    if (chatRow.AccessHash != accessHash)
                    {
                        return new SecretChatSendAppendResult(
                            SecretChatSendAppendStatus.AccessHashInvalid, null, null);
                    }
                    if ((SecretChatPersistenceState)chatRow.State !=
                        SecretChatPersistenceState.Active || !chatRow.Flags[1])
                    {
                        return new SecretChatSendAppendResult(
                            SecretChatSendAppendStatus.NotActive, null, null);
                    }
                    bool exactPair = senderAuthKeyId == chatRow.InitiatorAuthKeyId &&
                                     recipientAuthKeyId == chatRow.RecipientAuthKeyId ||
                                     senderAuthKeyId == chatRow.RecipientAuthKeyId &&
                                     recipientAuthKeyId == chatRow.InitiatorAuthKeyId;
                    if (!exactPair)
                    {
                        return new SecretChatSendAppendResult(
                            SecretChatSendAppendStatus.Unauthorized, null, null);
                    }

                    TLDto.TLSecretChatSendReceipt? existing =
                        await GetSendReceiptAsync(chatId, senderAuthKeyId, randomId,
                            cancellationToken: cancellationToken);
                    if (existing is not null)
                    {
                        int minimumReceiptDate = checked(date - receiptRetentionSeconds);
                        if (existing.Value.AsSecretChatSendReceipt().Date >=
                            minimumReceiptDate)
                        {
                            return new SecretChatSendAppendResult(
                                SecretChatSendAppendStatus.AlreadyExists, null,
                                existing.Value);
                        }
                        existing.Value.Dispose();
                        await _sendReceipts.DeleteAsync(chatId, senderAuthKeyId,
                            randomId);
                    }

                    TLDto.SecretChatQtsState stateRow = state.AsSecretChatQtsState();
                    int lastPersistedQts = stateRow.LastPersistedQts;
                    int acknowledgedQts = stateRow.AcknowledgedQts;
                    int queuedEvents = stateRow.QueuedEvents;
                    long queuedBytes = stateRow.QueuedBytes;
                    if (queuedEvents >= maxEvents)
                    {
                        return new SecretChatSendAppendResult(
                            SecretChatSendAppendStatus.EventLimitExceeded, null, null);
                    }
                    if (messageCopy.LongLength > maxBytes - queuedBytes)
                    {
                        return new SecretChatSendAppendResult(
                            SecretChatSendAppendStatus.ByteLimitExceeded, null, null);
                    }

                    using TLDto.TLSecretChatSendQtsPending pending =
                        BuildSendQtsPending(senderAuthKeyId, chatId, randomId, date,
                            expiresAt, messageCopy, resultCopy);
                    using TLDto.TLSecretChatQtsState reserved = BuildQtsState(
                        recipientAuthKeyId, lastPersistedQts, acknowledgedQts,
                        queuedEvents, queuedBytes, null, pending);
                    PutQtsState(reserved);
                    await FlushAsync("secret-chat send qts reservation");

                    int qts = await incrementQts();
                    if (qts <= lastPersistedQts)
                    {
                        throw new InvalidOperationException(
                            "The canonical qts counter did not advance.");
                    }
                    TLDto.TLSecretChatQtsEntry entry = BuildQtsEntry(
                        recipientAuthKeyId, qts, chatId, randomId, date, expiresAt,
                        messageCopy);
                    TLDto.TLSecretChatSendReceipt receipt;
                    try
                    {
                        receipt = BuildSendReceipt(chatId, senderAuthKeyId, randomId,
                            date, qts, resultCopy);
                    }
                    catch
                    {
                        entry.Dispose();
                        throw;
                    }
                    try
                    {
                        using TLDto.TLSecretChatQtsState next = BuildQtsState(
                            recipientAuthKeyId, qts, acknowledgedQts,
                            checked(queuedEvents + 1),
                            checked(queuedBytes + messageCopy.LongLength), null, null);
                        _qtsEntries.Put(entry.AsSpan().ToArray(), recipientAuthKeyId,
                            qts);
                        _sendReceipts.Put(receipt.AsSpan().ToArray(), chatId,
                            senderAuthKeyId, randomId);
                        PutQtsState(next);
                        await FlushAsync("secret-chat send qts entry");
                    }
                    catch
                    {
                        receipt.Dispose();
                        entry.Dispose();
                        throw;
                    }
                    return new SecretChatSendAppendResult(
                        SecretChatSendAppendStatus.Appended, entry, receipt);
                }
                finally
                {
                    chatGate.Release();
                }
            }
            finally
            {
                state.Dispose();
            }
        }
        finally
        {
            ReleaseGates(authGates);
        }
    }

    public async ValueTask<IReadOnlyList<TLDto.TLSecretChatQtsEntry>> GetQtsEntriesAsync(
        long recipientAuthKeyId, int afterQts = 0, int limit = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return [];
        }
        List<TLDto.TLSecretChatQtsEntry> entries = new();
        await foreach (byte[] bytes in _qtsEntries.IterateAsync(recipientAuthKeyId)
                           .WithCancellation(cancellationToken))
        {
            TLDto.TLSecretChatQtsEntry entry = ReadQtsEntry(bytes);
            if (entry.AsSecretChatQtsEntry().Qts > afterQts)
            {
                entries.Add(entry);
            }
        }
        return entries.OrderBy(x => x.AsSecretChatQtsEntry().Qts).Take(limit).ToArray();
    }

    public async ValueTask<TLDto.TLSecretChatQtsState> RecoverPendingQtsAsync(
        long recipientAuthKeyId, Func<ValueTask<int>> getCurrentQts,
        Func<ValueTask<int>> incrementQts,
        CancellationToken cancellationToken = default)
    {
        SemaphoreSlim gate = GetGate(_authKeyGates, recipientAuthKeyId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            TLDto.TLSecretChatQtsState state = await GetOrCreateQtsStateAsync(
                recipientAuthKeyId, cancellationToken);
            try
            {
                return await RecoverPendingQtsAppendAsync(state, getCurrentQts,
                    incrementQts);
            }
            catch
            {
                state.Dispose();
                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<TLDto.TLSecretChatQtsState> AcknowledgeQtsAsync(
        long recipientAuthKeyId, int maxQts,
        CancellationToken cancellationToken = default)
    {
        SemaphoreSlim gate = GetGate(_authKeyGates, recipientAuthKeyId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            TLDto.TLSecretChatQtsState state = await GetOrCreateQtsStateAsync(
                recipientAuthKeyId, cancellationToken);
            try
            {
                int lastPersistedQts = state.AsSecretChatQtsState().LastPersistedQts;
                int previousAcknowledgedQts =
                    state.AsSecretChatQtsState().AcknowledgedQts;
                int queuedEvents = state.AsSecretChatQtsState().QueuedEvents;
                long queuedBytes = state.AsSecretChatQtsState().QueuedBytes;
                int acknowledged = Math.Max(previousAcknowledgedQts,
                    Math.Min(maxQts, lastPersistedQts));
                IReadOnlyList<TLDto.TLSecretChatQtsEntry> entries =
                    await ReadQtsEntriesInternalAsync(recipientAuthKeyId,
                        cancellationToken);
                (int deletedEvents, long deletedBytes) = await DeleteQtsEntriesAsync(
                    recipientAuthKeyId, entries.Where(x =>
                        x.AsSecretChatQtsEntry().Qts <= acknowledged));
                TLDto.TLSecretChatQtsPending? pending = GetPendingAppend(state);
                TLDto.TLSecretChatSendQtsPending? pendingSend = GetPendingSend(state);
                TLDto.TLSecretChatQtsState next = BuildQtsState(recipientAuthKeyId,
                    lastPersistedQts, acknowledged,
                    Math.Max(0, queuedEvents - deletedEvents),
                    Math.Max(0, queuedBytes - deletedBytes), pending, pendingSend);
                try
                {
                    PutQtsState(next);
                    await FlushAsync("secret-chat qts acknowledgement");
                }
                catch
                {
                    next.Dispose();
                    throw;
                }
                return next;
            }
            finally
            {
                state.Dispose();
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<SecretChatQtsConfirmResult> ConfirmQtsAsync(
        long recipientAuthKeyId, int maxQts, Func<ValueTask<int>> getCurrentQts,
        Func<ValueTask<int>> incrementQts,
        CancellationToken cancellationToken = default)
    {
        SemaphoreSlim gate = GetGate(_authKeyGates, recipientAuthKeyId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            TLDto.TLSecretChatQtsState state = await GetOrCreateQtsStateAsync(
                recipientAuthKeyId, cancellationToken);
            try
            {
                state = await RecoverPendingQtsAppendAsync(state, getCurrentQts,
                    incrementQts);
                int acknowledgedQts = state.AsSecretChatQtsState().AcknowledgedQts;
                int lastPersistedQts = state.AsSecretChatQtsState().LastPersistedQts;
                bool valid = maxQts == acknowledgedQts ||
                             maxQts > acknowledgedQts && maxQts <= lastPersistedQts;
                if (!valid)
                {
                    return new SecretChatQtsConfirmResult(
                        SecretChatQtsConfirmStatus.Invalid, state);
                }
                if (maxQts == acknowledgedQts)
                {
                    return new SecretChatQtsConfirmResult(
                        SecretChatQtsConfirmStatus.Confirmed, state);
                }

                IReadOnlyList<TLDto.TLSecretChatQtsEntry> entries =
                    await ReadQtsEntriesInternalAsync(recipientAuthKeyId,
                        cancellationToken);
                (int deletedEvents, long deletedBytes) = await DeleteQtsEntriesAsync(
                    recipientAuthKeyId, entries.Where(x =>
                        x.AsSecretChatQtsEntry().Qts <= maxQts));
                TLDto.TLSecretChatQtsState confirmed = BuildQtsState(
                    recipientAuthKeyId, lastPersistedQts, maxQts,
                    Math.Max(0,
                        state.AsSecretChatQtsState().QueuedEvents - deletedEvents),
                    Math.Max(0,
                        state.AsSecretChatQtsState().QueuedBytes - deletedBytes),
                    null);
                try
                {
                    PutQtsState(confirmed);
                    await FlushAsync("secret-chat qts confirmation");
                }
                catch
                {
                    confirmed.Dispose();
                    throw;
                }
                state.Dispose();
                state = confirmed;
                return new SecretChatQtsConfirmResult(
                    SecretChatQtsConfirmStatus.Confirmed, confirmed);
            }
            catch
            {
                state.Dispose();
                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<SecretChatQtsDifferenceResult> ReadQtsDifferenceAsync(
        long recipientAuthKeyId, int requestQts, int now, int limit,
        Func<ValueTask<int>> getCurrentQts, Func<ValueTask<int>> incrementQts,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        SemaphoreSlim gate = GetGate(_authKeyGates, recipientAuthKeyId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            TLDto.TLSecretChatQtsState state = await GetOrCreateQtsStateAsync(
                recipientAuthKeyId, cancellationToken);
            try
            {
                state = await RecoverPendingQtsAppendAsync(state, getCurrentQts,
                    incrementQts);
                int lastPersistedQts = state.AsSecretChatQtsState().LastPersistedQts;
                int acknowledgedQts = state.AsSecretChatQtsState().AcknowledgedQts;
                int queuedEvents = state.AsSecretChatQtsState().QueuedEvents;
                long queuedBytes = state.AsSecretChatQtsState().QueuedBytes;
                List<TLDto.TLSecretChatQtsEntry> entries =
                    (await ReadQtsEntriesInternalAsync(recipientAuthKeyId,
                        cancellationToken)).ToList();
                var deletedQts = new HashSet<int>();
                long deletedBytes = 0;

                int expiryFloor = acknowledgedQts;
                foreach (TLDto.TLSecretChatQtsEntry entry in entries)
                {
                    int entryQts = entry.AsSecretChatQtsEntry().Qts;
                    if (entryQts <= expiryFloor)
                    {
                        continue;
                    }
                    if (entryQts != expiryFloor + 1 ||
                        entry.AsSecretChatQtsEntry().ExpiresAt > now)
                    {
                        break;
                    }
                    expiryFloor = entryQts;
                    deletedQts.Add(entryQts);
                    deletedBytes +=
                        entry.AsSecretChatQtsEntry().EncryptedMessage.Length;
                }

                int implicitAcknowledgement = Math.Max(expiryFloor,
                    Math.Min(requestQts, lastPersistedQts));
                foreach (TLDto.TLSecretChatQtsEntry entry in entries)
                {
                    int entryQts = entry.AsSecretChatQtsEntry().Qts;
                    if (entryQts <= implicitAcknowledgement && deletedQts.Add(entryQts))
                    {
                        deletedBytes +=
                            entry.AsSecretChatQtsEntry().EncryptedMessage.Length;
                    }
                }
                foreach (int qts in deletedQts)
                {
                    await _qtsEntries.DeleteAsync(recipientAuthKeyId, qts);
                }

                TLDto.TLSecretChatQtsState snapshotState = state;
                if (implicitAcknowledgement != acknowledgedQts || deletedQts.Count > 0)
                {
                    snapshotState = BuildQtsState(recipientAuthKeyId, lastPersistedQts,
                        implicitAcknowledgement,
                        Math.Max(0, queuedEvents - deletedQts.Count),
                        Math.Max(0, queuedBytes - deletedBytes), null);
                    try
                    {
                        PutQtsState(snapshotState);
                        await FlushAsync("secret-chat qts difference acknowledgement");
                    }
                    catch
                    {
                        snapshotState.Dispose();
                        throw;
                    }
                    state.Dispose();
                    state = snapshotState;
                }

                int selectionFloor = Math.Max(requestQts, implicitAcknowledgement);
                TLDto.TLSecretChatQtsEntry[] remaining = entries
                    .Where(x => !deletedQts.Contains(x.AsSecretChatQtsEntry().Qts) &&
                                x.AsSecretChatQtsEntry().Qts > selectionFloor)
                    .OrderBy(x => x.AsSecretChatQtsEntry().Qts)
                    .ToArray();
                return new SecretChatQtsDifferenceResult(
                    remaining.Take(limit).ToArray(), snapshotState, lastPersistedQts,
                    remaining.Length > limit);
            }
            catch
            {
                state.Dispose();
                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<IReadOnlyList<long>> GetQtsMaintenanceAuthKeyIdsAsync(
        long afterAuthKeyId, int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        var authKeyIds = new HashSet<long>();
        await foreach (byte[] bytes in _qtsStates.IterateAsync()
                           .WithCancellation(cancellationToken))
        {
            using TLDto.TLSecretChatQtsState state = ReadQtsState(bytes);
            long authKeyId = state.AsSecretChatQtsState().AuthKeyId;
            if (authKeyId > afterAuthKeyId)
            {
                authKeyIds.Add(authKeyId);
            }
        }
        return authKeyIds.Order().Take(limit).ToArray();
    }

    public async ValueTask<SecretChatQtsMaintenanceResult> MaintainQtsAsync(
        long recipientAuthKeyId, int now,
        Func<ValueTask<int>> getCurrentQts, Func<ValueTask<int>> incrementQts,
        CancellationToken cancellationToken = default)
    {
        SemaphoreSlim gate = GetGate(_authKeyGates, recipientAuthKeyId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            TLDto.TLSecretChatQtsState state = await GetOrCreateQtsStateAsync(
                recipientAuthKeyId, cancellationToken);
            try
            {
                TLDto.SecretChatQtsState before = state.AsSecretChatQtsState();
                bool recoveredPending = before.Flags[0] || before.Flags[1];
                state = await RecoverPendingQtsAppendAsync(state, getCurrentQts,
                    incrementQts);
                ExpiredQtsResult expiry = await ExpireContiguousQtsAsync(state,
                    recipientAuthKeyId, now, cancellationToken);
                state = expiry.State;
                TLDto.SecretChatQtsState row = state.AsSecretChatQtsState();
                return new SecretChatQtsMaintenanceResult(recipientAuthKeyId,
                    recoveredPending, expiry.Events, expiry.Bytes,
                    row.AcknowledgedQts, row.QueuedEvents, row.QueuedBytes);
            }
            finally
            {
                state.Dispose();
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<SecretChatRetentionCleanupResult> CleanupRetentionAsync(
        int now, int receiptRetentionSeconds, int maxItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(receiptRetentionSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxItems);
        int receiptCutoff = checked(now - receiptRetentionSeconds);
        var expiredReceipts = new List<(int ChatId, long SenderAuthKeyId,
            long RandomId)>();
        int scannedReceipts = 0;
        await foreach (byte[] bytes in _sendReceipts.IterateAsync()
                           .WithCancellation(cancellationToken))
        {
            if (scannedReceipts >= maxItems)
            {
                break;
            }
            scannedReceipts++;
            using TLDto.TLSecretChatSendReceipt receipt = ReadSendReceipt(bytes);
            TLDto.SecretChatSendReceipt row = receipt.AsSecretChatSendReceipt();
            if (row.Date <= receiptCutoff)
            {
                expiredReceipts.Add((row.ChatId, row.SenderAuthKeyId,
                    row.RandomId));
            }
        }

        int deletedReceipts = 0;
        foreach (var key in expiredReceipts)
        {
            SemaphoreSlim chatGate = GetGate(_chatGates, key.ChatId);
            await chatGate.WaitAsync(cancellationToken);
            try
            {
                byte[]? currentBytes = await _sendReceipts.GetAsync(key.ChatId,
                    key.SenderAuthKeyId, key.RandomId);
                if (currentBytes is not { Length: > 0 })
                {
                    continue;
                }
                using TLDto.TLSecretChatSendReceipt current =
                    ReadSendReceipt(currentBytes);
                if (current.AsSecretChatSendReceipt().Date <= receiptCutoff &&
                    await _sendReceipts.DeleteAsync(key.ChatId,
                        key.SenderAuthKeyId, key.RandomId))
                {
                    deletedReceipts++;
                }
            }
            finally
            {
                chatGate.Release();
            }
        }

        var expiredControls = new List<(long AuthKeyId, long UpdateId)>();
        int scannedControls = 0;
        await foreach (byte[] bytes in _controlUpdates.IterateAsync()
                           .WithCancellation(cancellationToken))
        {
            if (scannedControls >= maxItems)
            {
                break;
            }
            scannedControls++;
            using TLDto.TLSecretChatControlUpdate control =
                ReadControlUpdate(bytes);
            TLDto.SecretChatControlUpdate row = control.AsSecretChatControlUpdate();
            if (row.ExpiresAt <= now)
            {
                expiredControls.Add((row.RecipientAuthKeyId, row.UpdateId));
            }
        }

        int deletedControls = 0;
        foreach (var key in expiredControls)
        {
            SemaphoreSlim authGate = GetGate(_authKeyGates, key.AuthKeyId);
            await authGate.WaitAsync(cancellationToken);
            try
            {
                byte[]? currentBytes = await _controlUpdates.GetAsync(key.AuthKeyId,
                    key.UpdateId);
                if (currentBytes is not { Length: > 0 })
                {
                    continue;
                }
                using TLDto.TLSecretChatControlUpdate current =
                    ReadControlUpdate(currentBytes);
                if (current.AsSecretChatControlUpdate().ExpiresAt <= now &&
                    await _controlUpdates.DeleteAsync(key.AuthKeyId, key.UpdateId))
                {
                    deletedControls++;
                }
            }
            finally
            {
                authGate.Release();
            }
        }

        if (deletedReceipts != 0 || deletedControls != 0)
        {
            await FlushAsync("secret-chat retention cleanup");
        }
        return new SecretChatRetentionCleanupResult(scannedReceipts,
            deletedReceipts, scannedControls, deletedControls);
    }

    public async ValueTask<bool> PutControlUpdateAsync(
        TLDto.TLSecretChatControlUpdate update,
        CancellationToken cancellationToken = default)
    {
        long recipientAuthKeyId = update.AsSecretChatControlUpdate().RecipientAuthKeyId;
        long updateId = update.AsSecretChatControlUpdate().UpdateId;
        SemaphoreSlim gate = GetGate(_authKeyGates, recipientAuthKeyId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (await IsRevokedAsync(recipientAuthKeyId))
            {
                return false;
            }
            byte[]? existing = await _controlUpdates.GetAsync(recipientAuthKeyId,
                updateId);
            if (existing is { Length: > 0 })
            {
                return ControlUpdatesEqual(ReadControlUpdate(existing), update);
            }
            bool result = _controlUpdates.Put(update.AsSpan().ToArray(),
                recipientAuthKeyId, updateId);
            await FlushAsync("secret-chat control update");
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<IReadOnlyList<TLDto.TLSecretChatControlUpdate>>
        GetControlUpdatesAsync(long recipientAuthKeyId,
            CancellationToken cancellationToken = default)
    {
        List<TLDto.TLSecretChatControlUpdate> updates = new();
        await foreach (byte[] bytes in _controlUpdates.IterateAsync(recipientAuthKeyId)
                           .WithCancellation(cancellationToken))
        {
            updates.Add(ReadControlUpdate(bytes));
        }
        return updates.OrderBy(x => x.AsSecretChatControlUpdate().Date)
            .ThenBy(x => x.AsSecretChatControlUpdate().UpdateId).ToArray();
    }

    public async ValueTask<bool> DeleteControlUpdatesAsync(long recipientAuthKeyId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool result = await _controlUpdates.DeleteAsync(recipientAuthKeyId);
        await FlushAsync("secret-chat control-update deletion");
        return result;
    }

    public async ValueTask<SecretChatControlDifferenceResult>
        GetControlDifferenceAsync(long recipientAuthKeyId, int requestDate,
            int responseDate, int now, bool isProbe,
            CancellationToken cancellationToken = default)
    {
        if (isProbe)
        {
            return new SecretChatControlDifferenceResult([]);
        }

        SemaphoreSlim gate = GetGate(_authKeyGates, recipientAuthKeyId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var offered = new List<TLDto.TLSecretChatControlUpdate>();
            try
            {
                bool changed = false;
                await foreach (byte[] bytes in _controlUpdates.IterateAsync(
                                   recipientAuthKeyId)
                                   .WithCancellation(cancellationToken))
                {
                    TLDto.TLSecretChatControlUpdate update = ReadControlUpdate(bytes);
                    long updateId = update.AsSecretChatControlUpdate().UpdateId;
                    bool confirmed = update.AsSecretChatControlUpdate().Flags[0] &&
                                     update.AsSecretChatControlUpdate()
                                         .OfferedStateDate <= requestDate;
                    if (update.AsSecretChatControlUpdate().ExpiresAt <= now ||
                        confirmed)
                    {
                        await _controlUpdates.DeleteAsync(recipientAuthKeyId,
                            updateId);
                        changed = true;
                        continue;
                    }

                    TLDto.TLSecretChatControlUpdate next = BuildControlUpdate(update,
                        responseDate);
                    offered.Add(next);
                    _controlUpdates.Put(next.AsSpan().ToArray(), recipientAuthKeyId,
                        next.AsSecretChatControlUpdate().UpdateId);
                    changed = true;
                }
                if (changed)
                {
                    await FlushAsync("secret-chat control difference");
                }
                return new SecretChatControlDifferenceResult(offered
                    .OrderBy(x => x.AsSecretChatControlUpdate().Date)
                    .ThenBy(x => x.AsSecretChatControlUpdate().UpdateId)
                    .ToArray());
            }
            catch
            {
                foreach (TLDto.TLSecretChatControlUpdate update in offered)
                {
                    update.Dispose();
                }
                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<bool> PutEncryptedFileAsync(
        TLDto.TLSecretChatEncryptedFile file,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long fileId = file.AsSecretChatEncryptedFile().FileId;
        long accessHash = file.AsSecretChatEncryptedFile().AccessHash;
        long uploadFileId = file.AsSecretChatEncryptedFile().UploadFileId;
        bool result = _encryptedFiles.Put(file.AsSpan().ToArray(), fileId,
            accessHash, uploadFileId);
        await FlushAsync("secret-chat encrypted-file metadata");
        return result;
    }

    public async ValueTask<TLDto.TLSecretChatEncryptedFile?> GetEncryptedFileAsync(
        long fileId,
        long accessHash, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? bytes = await _encryptedFiles.GetBySecondaryIndexAsync(
            "by_exact", fileId, accessHash);
        return bytes is { Length: > 0 }
            ? ReadEncryptedFile(bytes)
            : null;
    }

    public async ValueTask<TLDto.TLSecretChatEncryptedFile?>
        GetEncryptedFileByIdAsync(long fileId,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? bytes = await _encryptedFiles.GetBySecondaryIndexAsync(
            "by_id", fileId);
        return bytes is { Length: > 0 }
            ? ReadEncryptedFile(bytes)
            : null;
    }

    public async ValueTask<TLDto.TLSecretChatEncryptedFile?>
        GetEncryptedFileByUploadIdAsync(long uploadFileId,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? bytes = await _encryptedFiles.GetBySecondaryIndexAsync(
            "by_upload", uploadFileId);
        return bytes is { Length: > 0 }
            ? ReadEncryptedFile(bytes)
            : null;
    }

    public async ValueTask<SecretChatFileAssociationStatus>
        TryAssociateEncryptedFileAsync(
            TLDto.TLSecretChatEncryptedFileAssociation association,
            int maxAssociations, CancellationToken cancellationToken = default)
    {
        long fileId = association.AsSecretChatEncryptedFileAssociation().FileId;
        int chatId = association.AsSecretChatEncryptedFileAssociation().ChatId;
        SemaphoreSlim gate = GetGate(_fileGates, fileId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            byte[]? existing = await _encryptedFileAssociations.GetAsync(
                fileId, chatId);
            if (existing is { Length: > 0 })
            {
                return SecretChatFileAssociationStatus.AlreadyExists;
            }
            IReadOnlyList<TLDto.TLSecretChatEncryptedFileAssociation> associations =
                await GetEncryptedFileAssociationsAsync(fileId,
                    cancellationToken);
            if (associations.Count >= maxAssociations)
            {
                return SecretChatFileAssociationStatus.LimitExceeded;
            }
            _encryptedFileAssociations.Put(association.AsSpan().ToArray(), fileId,
                chatId);
            await FlushAsync("secret-chat encrypted-file association");
            return SecretChatFileAssociationStatus.Created;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<IReadOnlyList<TLDto.TLSecretChatEncryptedFileAssociation>>
        GetEncryptedFileAssociationsAsync(long fileId,
            CancellationToken cancellationToken = default)
    {
        List<TLDto.TLSecretChatEncryptedFileAssociation> associations = new();
        await foreach (byte[] bytes in _encryptedFileAssociations.IterateAsync(fileId)
                           .WithCancellation(cancellationToken))
        {
            associations.Add(ReadEncryptedFileAssociation(bytes));
        }
        return associations.OrderBy(x =>
            x.AsSecretChatEncryptedFileAssociation().ChatId).ToArray();
    }

    public async ValueTask<IReadOnlyList<TLDto.TLSecretChatEncryptedFileAssociation>>
        GetEncryptedFilesByChatAsync(int chatId,
            CancellationToken cancellationToken = default)
    {
        List<TLDto.TLSecretChatEncryptedFileAssociation> associations = new();
        await foreach (byte[] bytes in _encryptedFileAssociations
                           .IterateBySecondaryIndexAsync("by_chat", chatId)
                           .WithCancellation(cancellationToken))
        {
            associations.Add(ReadEncryptedFileAssociation(bytes));
        }
        return associations.OrderBy(x =>
            x.AsSecretChatEncryptedFileAssociation().FileId).ToArray();
    }

    public async ValueTask<SecretChatAuthKeyRevocationResult> RevokeAuthKeyAsync(
        long authKeyId, int date, CancellationToken cancellationToken = default)
    {
        SemaphoreSlim authGate = GetGate(_authKeyGates, authKeyId);
        await authGate.WaitAsync(cancellationToken);
        try
        {
            TLDto.TLSecretChatAuthKeyRevocation? previousRevocation =
                await GetRevocationAsync(authKeyId);
            if (previousRevocation is not null)
            {
                TLDto.TLSecretChatAuthKeyRevocation stored = previousRevocation.Value;
                return new SecretChatAuthKeyRevocationResult(true,
                    stored.AsSecretChatAuthKeyRevocation().Date,
                    stored.AsSecretChatAuthKeyRevocation().NotificationsCompleted
                        ? []
                        : AffectedPeers(stored));
            }

            Dictionary<int, TLDto.TLSecretChatState> affectedChats = new();
            foreach (TLDto.TLSecretChatState chat in
                     await GetChatsByAuthKeyAsync(authKeyId,
                         cancellationToken))
            {
                affectedChats[ChatId(chat)] = chat;
            }
            await foreach (byte[] bytes in _requestedDevices.IterateAsync(authKeyId)
                               .WithCancellation(cancellationToken))
            {
                TLDto.TLSecretChatRequestedDevice target = ReadRequestedDevice(bytes);
                int targetChatId = target.AsSecretChatRequestedDevice().ChatId;
                TLDto.TLSecretChatState? chat = await GetChatInternalAsync(targetChatId,
                    cancellationToken);
                if (chat is not null)
                {
                    affectedChats[ChatId(chat.Value)] = chat.Value;
                }
            }

            List<TLDto.TLSecretChatRevokedPeer> peers = new();
            List<TLDto.TLSecretChatRevokedPeer> uniquePeers = new();
            try
            {
                foreach (int chatId in affectedChats.Keys.Order())
                {
                    SemaphoreSlim chatGate = GetGate(_chatGates, chatId);
                    await chatGate.WaitAsync(cancellationToken);
                    try
                    {
                        TLDto.TLSecretChatState? chat = await GetChatInternalAsync(
                            chatId, cancellationToken);
                        if (chat is null ||
                            State(chat.Value) == SecretChatPersistenceState.Discarded)
                        {
                            continue;
                        }
                        TLDto.TLSecretChatState existingChat = chat.Value;
                        SecretChatPersistenceState state = State(existingChat);
                        long initiatorKey = InitiatorAuthKeyId(existingChat);
                        long? recipientKey = RecipientAuthKeyId(existingChat);

                        if (initiatorKey == authKeyId)
                        {
                            await DeleteControlTargetsByChatAsync(chatId,
                                cancellationToken);
                            if (state == SecretChatPersistenceState.Active &&
                                recipientKey is long activeRecipientKey)
                            {
                                peers.Add(BuildRevokedPeer(chatId, activeRecipientKey,
                                    RecipientUserId(existingChat), 0));
                            }
                            else
                            {
                                foreach (long requestedKey in
                                         RequestedRecipientAuthKeyIds(existingChat)
                                             .Distinct())
                                {
                                    peers.Add(BuildRevokedPeer(chatId, requestedKey,
                                        RecipientUserId(existingChat), 0));
                                }
                            }
                            using TLDto.TLSecretChatState discarded =
                                BuildDiscardedChat(existingChat, false, date);
                            ReplaceChat(existingChat, discarded);
                            DeleteRequestedDevices(existingChat);
                            continue;
                        }

                        if (state == SecretChatPersistenceState.Active &&
                            recipientKey == authKeyId)
                        {
                            await DeleteControlTargetsByChatAsync(chatId,
                                cancellationToken);
                            peers.Add(BuildRevokedPeer(chatId, initiatorKey,
                                InitiatorUserId(existingChat), 0));
                            using TLDto.TLSecretChatState discarded =
                                BuildDiscardedChat(existingChat, false, date);
                            ReplaceChat(existingChat, discarded);
                            continue;
                        }

                        long[] requestedKeys =
                            RequestedRecipientAuthKeyIds(existingChat);
                        if (state == SecretChatPersistenceState.Pending &&
                            requestedKeys.Contains(authKeyId))
                        {
                            _controlTargets.Delete(authKeyId, chatId);
                            long[] remaining = requestedKeys
                                .Where(x => x != authKeyId).Distinct().ToArray();
                            _requestedDevices.Delete(authKeyId, chatId);
                            if (remaining.Length == 0)
                            {
                                await DeleteControlTargetsByChatAsync(chatId,
                                    cancellationToken);
                                peers.Add(BuildRevokedPeer(chatId, initiatorKey,
                                    InitiatorUserId(existingChat), 0));
                                using TLDto.TLSecretChatState discarded =
                                    BuildDiscardedChat(existingChat, false, date);
                                ReplaceChat(existingChat, discarded);
                            }
                            else
                            {
                                using TLDto.TLSecretChatState detached =
                                    BuildPendingWithRequestedKeys(existingChat, date,
                                        remaining);
                                ReplaceChat(existingChat, detached);
                            }
                        }
                    }
                    finally
                    {
                        chatGate.Release();
                    }
                }

                await _qtsEntries.DeleteAsync(authKeyId);
                await _qtsStates.DeleteAsync(authKeyId);
                await _controlUpdates.DeleteAsync(authKeyId);
                await _controlTargets.DeleteAsync(authKeyId);
                await _requestedDevices.DeleteAsync(authKeyId);
                foreach (TLDto.TLSecretChatRevokedPeer peer in peers.DistinctBy(
                             x => (x.AsSecretChatRevokedPeer().ChatId,
                                 x.AsSecretChatRevokedPeer().PeerAuthKeyId)))
                {
                    TLDto.SecretChatRevokedPeer row = peer.AsSecretChatRevokedPeer();
                    uniquePeers.Add(BuildRevokedPeer(row.ChatId, row.PeerAuthKeyId,
                        row.PeerUserId, CreateControlUpdateId()));
                }
                using TLDto.TLSecretChatAuthKeyRevocation tombstone =
                    BuildRevocation(authKeyId, date, false, uniquePeers);
                _revocations.Put(tombstone.AsSpan().ToArray(), authKeyId);
                await FlushAsync("secret-chat auth-key revocation");
                return new SecretChatAuthKeyRevocationResult(false, date,
                    uniquePeers);
            }
            catch
            {
                foreach (TLDto.TLSecretChatRevokedPeer peer in uniquePeers)
                {
                    peer.Dispose();
                }
                throw;
            }
            finally
            {
                foreach (TLDto.TLSecretChatRevokedPeer peer in peers)
                {
                    peer.Dispose();
                }
            }
        }
        finally
        {
            authGate.Release();
        }
    }

    public async ValueTask<bool> CompleteAuthKeyRevocationAsync(long authKeyId,
        CancellationToken cancellationToken = default)
    {
        SemaphoreSlim gate = GetGate(_authKeyGates, authKeyId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            TLDto.TLSecretChatAuthKeyRevocation? tombstone =
                await GetRevocationAsync(authKeyId);
            if (tombstone is null)
            {
                return false;
            }
            if (!tombstone.Value.AsSecretChatAuthKeyRevocation().NotificationsCompleted)
            {
                using TLDto.TLSecretChatAuthKeyRevocation completed = BuildRevocation(
                    authKeyId, tombstone.Value.AsSecretChatAuthKeyRevocation().Date,
                    true, AffectedPeers(tombstone.Value));
                _revocations.Put(completed.AsSpan().ToArray(), authKeyId);
                await FlushAsync("secret-chat auth-key revocation completion");
            }
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask<TLDto.TLSecretChatState?> GetChatInternalAsync(int chatId,
        CancellationToken cancellationToken)
    {
        await foreach (byte[] bytes in _chats.IterateAsync(chatId)
                           .WithCancellation(cancellationToken))
        {
            return ReadChat(bytes);
        }
        return null;
    }

    private async ValueTask<TLDto.TLSecretChatState?>
        GetChatByInitiatorRandomIdInternalAsync(long initiatorUserId, int randomId,
            CancellationToken cancellationToken)
    {
        await foreach (byte[] bytes in _chats.IterateBySecondaryIndexAsync(
                           "by_initiator_random_id", initiatorUserId, randomId)
                           .WithCancellation(cancellationToken))
        {
            return ReadChat(bytes);
        }
        return null;
    }

    private async ValueTask<TLDto.TLSecretChatPendingPair?> GetPendingPairAsync(
        long initiatorUserId, long recipientUserId)
    {
        byte[]? bytes = await _pendingPairs.GetAsync(initiatorUserId,
            recipientUserId);
        return bytes is { Length: > 0 }
            ? new TLDto.TLSecretChatPendingPair(bytes, 0, bytes.Length)
            : null;
    }

    private async ValueTask<TLDto.TLSecretChatState?> GetPendingChatForPairAsync(
        long initiatorUserId, long recipientUserId,
        CancellationToken cancellationToken)
    {
        await foreach (byte[] bytes in _chats.IterateBySecondaryIndexAsync(
                           "by_initiator_user", initiatorUserId)
                           .WithCancellation(cancellationToken))
        {
            TLDto.TLSecretChatState chat = ReadChat(bytes);
            if (RecipientUserId(chat) == recipientUserId &&
                State(chat) == SecretChatPersistenceState.Pending)
            {
                return chat;
            }
            chat.Dispose();
        }
        return null;
    }

    private async ValueTask<int> CountChatsByStateAsync(long authKeyId,
        SecretChatPersistenceState state, CancellationToken cancellationToken)
    {
        IReadOnlyList<TLDto.TLSecretChatState> chats = await GetChatsByAuthKeyAsync(
            authKeyId, cancellationToken);
        int count = 0;
        foreach (TLDto.TLSecretChatState chat in chats)
        {
            using (chat)
            {
                if (State(chat) == state)
                {
                    count++;
                }
            }
        }
        return count;
    }

    private async ValueTask<int> CountRequestedChatsAsync(long authKeyId,
        CancellationToken cancellationToken)
    {
        int count = 0;
        await foreach (byte[] _ in _requestedDevices.IterateAsync(authKeyId)
                           .WithCancellation(cancellationToken))
        {
            count++;
        }
        return count;
    }

    private async ValueTask<TLDto.TLSecretChatQtsState> GetOrCreateQtsStateAsync(
        long authKeyId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? bytes = await _qtsStates.GetAsync(authKeyId);
        if (bytes is { Length: > 0 })
        {
            return ReadQtsState(bytes);
        }
        TLDto.TLSecretChatQtsState seed = BuildQtsState(authKeyId, 1, 1, 0, 0,
            null);
        try
        {
            PutQtsState(seed);
        }
        catch
        {
            seed.Dispose();
            throw;
        }
        return seed;
    }

    private async ValueTask<TLDto.TLSecretChatQtsState> RecoverPendingQtsAppendAsync(
        TLDto.TLSecretChatQtsState state, Func<ValueTask<int>> getCurrentQts,
        Func<ValueTask<int>> incrementQts)
    {
        TLDto.TLSecretChatQtsPending? pending = GetPendingAppend(state);
        TLDto.TLSecretChatSendQtsPending? pendingSend = GetPendingSend(state);
        if (pending is null && pendingSend is null)
        {
            return state;
        }

        long authKeyId = state.AsSecretChatQtsState().AuthKeyId;
        int lastPersistedQts = state.AsSecretChatQtsState().LastPersistedQts;
        int acknowledgedQts = state.AsSecretChatQtsState().AcknowledgedQts;
        int queuedEvents = state.AsSecretChatQtsState().QueuedEvents;
        long queuedBytes = state.AsSecretChatQtsState().QueuedBytes;
        int chatId;
        long randomId;
        int date;
        int expiresAt;
        byte[] encryptedMessage;
        long? senderAuthKeyId = null;
        byte[]? result = null;
        if (pendingSend is not null)
        {
            TLDto.SecretChatSendQtsPending row =
                pendingSend.Value.AsSecretChatSendQtsPending();
            senderAuthKeyId = row.SenderAuthKeyId;
            chatId = row.ChatId;
            randomId = row.RandomId;
            date = row.Date;
            expiresAt = row.ExpiresAt;
            encryptedMessage = row.EncryptedMessage.ToArray();
            result = row.Result.ToArray();
        }
        else
        {
            TLDto.SecretChatQtsPending row = pending!.Value.AsSecretChatQtsPending();
            chatId = row.ChatId;
            randomId = row.RandomId;
            date = row.Date;
            expiresAt = row.ExpiresAt;
            encryptedMessage = row.EncryptedMessage.ToArray();
        }

        int? recoveredQts = null;
        IReadOnlyList<TLDto.TLSecretChatQtsEntry> existingEntries =
            await ReadQtsEntriesInternalAsync(authKeyId, CancellationToken.None);
        foreach (TLDto.TLSecretChatQtsEntry existingEntry in existingEntries)
        {
            using (existingEntry)
            {
                TLDto.SecretChatQtsEntry row = existingEntry.AsSecretChatQtsEntry();
                if (row.ChatId == chatId && row.RandomId == randomId &&
                    row.Date == date && row.EncryptedMessage.SequenceEqual(
                        encryptedMessage))
                {
                    recoveredQts = row.Qts;
                }
            }
        }

        if (senderAuthKeyId is long sender)
        {
            TLDto.TLSecretChatSendReceipt? existingReceipt =
                await GetSendReceiptAsync(chatId, sender, randomId);
            if (existingReceipt is not null)
            {
                using TLDto.TLSecretChatSendReceipt receipt = existingReceipt.Value;
                recoveredQts ??= receipt.AsSecretChatSendReceipt().RecipientQts;
            }
        }

        int qts;
        if (recoveredQts is int existingQts && existingQts > lastPersistedQts)
        {
            qts = existingQts;
        }
        else
        {
            int currentQts = await getCurrentQts();
            qts = currentQts > lastPersistedQts
                ? currentQts
                : await incrementQts();
        }
        if (qts <= lastPersistedQts)
        {
            throw new InvalidOperationException(
                "The canonical qts counter did not advance during recovery.");
        }

        using TLDto.TLSecretChatQtsEntry entry = BuildQtsEntry(authKeyId, qts,
            chatId, randomId, date, expiresAt, encryptedMessage);
        TLDto.TLSecretChatQtsState recovered = BuildQtsState(authKeyId, qts,
            acknowledgedQts, checked(queuedEvents + 1),
            checked(queuedBytes + encryptedMessage.LongLength), null, null);
        try
        {
            _qtsEntries.Put(entry.AsSpan().ToArray(), authKeyId, qts);
            if (senderAuthKeyId is long senderKey)
            {
                using TLDto.TLSecretChatSendReceipt receipt = BuildSendReceipt(chatId,
                    senderKey, randomId, date, qts, result!);
                _sendReceipts.Put(receipt.AsSpan().ToArray(), chatId, senderKey,
                    randomId);
            }
            PutQtsState(recovered);
            await FlushAsync("secret-chat qts reservation recovery");
        }
        catch
        {
            recovered.Dispose();
            throw;
        }
        state.Dispose();
        return recovered;
    }

    private async ValueTask<IReadOnlyList<TLDto.TLSecretChatQtsEntry>>
        ReadQtsEntriesInternalAsync(long recipientAuthKeyId,
            CancellationToken cancellationToken)
    {
        var entries = new List<TLDto.TLSecretChatQtsEntry>();
        await foreach (byte[] bytes in _qtsEntries.IterateAsync(recipientAuthKeyId)
                           .WithCancellation(cancellationToken))
        {
            entries.Add(ReadQtsEntry(bytes));
        }
        return entries.OrderBy(x => x.AsSecretChatQtsEntry().Qts).ToArray();
    }

    private async ValueTask<ExpiredQtsResult> ExpireContiguousQtsAsync(
        TLDto.TLSecretChatQtsState state, long recipientAuthKeyId, int now,
        CancellationToken cancellationToken)
    {
        int lastPersistedQts = state.AsSecretChatQtsState().LastPersistedQts;
        int currentAcknowledgedQts = state.AsSecretChatQtsState().AcknowledgedQts;
        int queuedEvents = state.AsSecretChatQtsState().QueuedEvents;
        long queuedBytes = state.AsSecretChatQtsState().QueuedBytes;
        TLDto.TLSecretChatQtsPending? pending = GetPendingAppend(state);
        TLDto.TLSecretChatSendQtsPending? pendingSend = GetPendingSend(state);
        IReadOnlyList<TLDto.TLSecretChatQtsEntry> entries =
            await ReadQtsEntriesInternalAsync(recipientAuthKeyId,
                cancellationToken);
        var expired = new List<TLDto.TLSecretChatQtsEntry>();
        int nextQts = currentAcknowledgedQts + 1;
        try
        {
            foreach (TLDto.TLSecretChatQtsEntry entry in entries)
            {
                TLDto.SecretChatQtsEntry entryRow =
                    entry.AsSecretChatQtsEntry();
                if (entryRow.Qts <= currentAcknowledgedQts)
                {
                    continue;
                }
                if (entryRow.Qts != nextQts || entryRow.ExpiresAt > now)
                {
                    break;
                }
                expired.Add(entry);
                nextQts++;
            }

            if (expired.Count == 0)
            {
                return new ExpiredQtsResult(state, 0, 0);
            }

            int expectedEvents = expired.Count;
            long expectedBytes = 0;
            foreach (TLDto.TLSecretChatQtsEntry entry in expired)
            {
                expectedBytes = checked(expectedBytes + entry
                    .AsSecretChatQtsEntry().EncryptedMessage.Length);
            }
            int acknowledgedQts = nextQts - 1;
            int remainingEvents = checked(queuedEvents - expectedEvents);
            long remainingBytes = checked(queuedBytes - expectedBytes);
            if (remainingEvents < 0 || remainingBytes < 0)
            {
                throw new InvalidDataException(
                    "Secret-chat qts counters are inconsistent with durable entries.");
            }
            (int events, long bytes) = await DeleteQtsEntriesAsync(
                recipientAuthKeyId, expired);
            TLDto.TLSecretChatQtsState next = BuildQtsState(recipientAuthKeyId,
                lastPersistedQts, acknowledgedQts,
                remainingEvents, remainingBytes, pending, pendingSend);
            try
            {
                PutQtsState(next);
                await FlushAsync("secret-chat qts expiry");
            }
            catch
            {
                next.Dispose();
                throw;
            }
            state.Dispose();
            return new ExpiredQtsResult(next, events, bytes);
        }
        finally
        {
            foreach (TLDto.TLSecretChatQtsEntry entry in entries)
            {
                entry.Dispose();
            }
        }
    }

    private async ValueTask<(int Events, long Bytes)> DeleteQtsEntriesAsync(
        long recipientAuthKeyId,
        IEnumerable<TLDto.TLSecretChatQtsEntry> entries)
    {
        int deletedEvents = 0;
        long deletedBytes = 0;
        foreach (TLDto.TLSecretChatQtsEntry entry in entries)
        {
            int qts = entry.AsSecretChatQtsEntry().Qts;
            deletedBytes += entry.AsSecretChatQtsEntry().EncryptedMessage.Length;
            await _qtsEntries.DeleteAsync(recipientAuthKeyId, qts);
            deletedEvents++;
        }
        return (deletedEvents, deletedBytes);
    }

    private void PutQtsState(TLDto.TLSecretChatQtsState state) =>
        _qtsStates.Put(state.AsSpan().ToArray(),
            state.AsSecretChatQtsState().AuthKeyId);

    private readonly record struct ExpiredQtsResult(
        TLDto.TLSecretChatQtsState State, int Events, long Bytes);

    private async ValueTask<bool> IsRevokedAsync(long authKeyId)
    {
        return await GetRevocationAsync(authKeyId) is not null;
    }

    private async ValueTask<TLDto.TLSecretChatAuthKeyRevocation?> GetRevocationAsync(
        long authKeyId)
    {
        byte[]? bytes = await _revocations.GetAsync(authKeyId);
        return bytes is { Length: > 0 }
            ? ReadRevocation(bytes)
            : null;
    }

    private void ReplaceChat(TLDto.TLSecretChatState previous,
        TLDto.TLSecretChatState next)
    {
        DeleteChat(previous);
        PutChat(next);
        if (State(previous) == SecretChatPersistenceState.Pending &&
            State(next) != SecretChatPersistenceState.Pending)
        {
            _pendingPairs.Delete(InitiatorUserId(previous),
                RecipientUserId(previous));
        }
    }

    private void PutChat(TLDto.TLSecretChatState chat)
    {
        _chats.Put(chat.AsSpan().ToArray(), ChatId(chat), InitiatorAuthKeyId(chat),
            RecipientAuthKeyId(chat) ?? 0L, InitiatorUserId(chat), ChatId(chat));
    }

    private void DeleteChat(TLDto.TLSecretChatState chat)
    {
        _chats.Delete(ChatId(chat), InitiatorAuthKeyId(chat),
            RecipientAuthKeyId(chat) ?? 0L, InitiatorUserId(chat), ChatId(chat));
    }

    private void DeleteRequestedDevices(TLDto.TLSecretChatState chat)
    {
        foreach (long authKeyId in RequestedRecipientAuthKeyIds(chat).Distinct())
        {
            _requestedDevices.Delete(authKeyId, ChatId(chat));
        }
    }

    private async ValueTask DeleteControlTargetsByChatAsync(int chatId,
        CancellationToken cancellationToken)
    {
        long[] authKeyIds = (await GetControlTargetAuthKeyIdsAsync(chatId,
            cancellationToken)).ToArray();
        foreach (long authKeyId in authKeyIds)
        {
            _controlTargets.Delete(authKeyId, chatId);
        }
    }

    private static TLDto.TLSecretChatState BuildPendingChat(
        TLDto.TLSecretChatState source, IReadOnlyList<long> requestedAuthKeys)
    {
        TLDto.SecretChatState row = source.AsSecretChatState();
        return BuildChat(row.HistoryDeleted, null, null, null, row.ChatId,
            row.AccessHash, row.InitiatorUserId, row.RecipientUserId,
            row.InitiatorAuthKeyId, SecretChatPersistenceState.Pending, row.CreatedAt,
            row.UpdatedAt, row.GA.ToArray(), row.InitiatorReadMaxDate,
            row.RecipientReadMaxDate, requestedAuthKeys);
    }

    private static TLDto.TLSecretChatState BuildAcceptedChat(
        TLDto.TLSecretChatState source, long recipientAuthKeyId,
        ReadOnlySpan<byte> gB, long keyFingerprint, int date)
    {
        TLDto.SecretChatState row = source.AsSecretChatState();
        return BuildChat(row.HistoryDeleted, recipientAuthKeyId, gB.ToArray(),
            keyFingerprint, row.ChatId, row.AccessHash, row.InitiatorUserId,
            row.RecipientUserId, row.InitiatorAuthKeyId,
            SecretChatPersistenceState.Active, row.CreatedAt, date,
            row.GA.ToArray(), row.InitiatorReadMaxDate, row.RecipientReadMaxDate,
            row.RequestedRecipientAuthKeyIds.ToArray());
    }

    private static TLDto.TLSecretChatState BuildDiscardedChat(
        TLDto.TLSecretChatState source, bool deleteHistory, int date)
    {
        TLDto.SecretChatState row = source.AsSecretChatState();
        long? recipientAuthKeyId = row.Flags[1] ? row.RecipientAuthKeyId : null;
        byte[]? gB = row.Flags[2] ? row.GB.ToArray() : null;
        long? keyFingerprint = row.Flags[3] ? row.KeyFingerprint : null;
        return BuildChat(row.HistoryDeleted || deleteHistory, recipientAuthKeyId, gB,
            keyFingerprint, row.ChatId, row.AccessHash, row.InitiatorUserId,
            row.RecipientUserId, row.InitiatorAuthKeyId,
            SecretChatPersistenceState.Discarded, row.CreatedAt, date,
            row.GA.ToArray(), row.InitiatorReadMaxDate, row.RecipientReadMaxDate,
            row.RequestedRecipientAuthKeyIds.ToArray());
    }

    private static TLDto.TLSecretChatState BuildReadChat(
        TLDto.TLSecretChatState source, bool callerIsInitiator, int maxDate)
    {
        TLDto.SecretChatState row = source.AsSecretChatState();
        return BuildChat(row.HistoryDeleted,
            row.Flags[1] ? row.RecipientAuthKeyId : null,
            row.Flags[2] ? row.GB.ToArray() : null,
            row.Flags[3] ? row.KeyFingerprint : null, row.ChatId, row.AccessHash,
            row.InitiatorUserId, row.RecipientUserId, row.InitiatorAuthKeyId,
            (SecretChatPersistenceState)row.State, row.CreatedAt, row.UpdatedAt,
            row.GA.ToArray(),
            callerIsInitiator ? maxDate : row.InitiatorReadMaxDate,
            callerIsInitiator ? row.RecipientReadMaxDate : maxDate,
            row.RequestedRecipientAuthKeyIds.ToArray());
    }

    private static TLDto.TLSecretChatState BuildPendingWithRequestedKeys(
        TLDto.TLSecretChatState source, int date, IReadOnlyList<long> requestedAuthKeys)
    {
        TLDto.SecretChatState row = source.AsSecretChatState();
        return BuildChat(row.HistoryDeleted, null, null, null, row.ChatId,
            row.AccessHash, row.InitiatorUserId, row.RecipientUserId,
            row.InitiatorAuthKeyId, SecretChatPersistenceState.Pending, row.CreatedAt,
            date, row.GA.ToArray(), row.InitiatorReadMaxDate,
            row.RecipientReadMaxDate, requestedAuthKeys);
    }

    private static TLDto.TLSecretChatState BuildChat(bool historyDeleted,
        long? recipientAuthKeyId, byte[]? gB, long? keyFingerprint, int chatId,
        long accessHash, long initiatorUserId, long recipientUserId,
        long initiatorAuthKeyId, SecretChatPersistenceState state, int createdAt,
        int updatedAt, byte[] gA, int initiatorReadMaxDate, int recipientReadMaxDate,
        IReadOnlyList<long> requestedAuthKeys)
    {
        var keys = new VectorOfLong();
        foreach (long authKeyId in requestedAuthKeys)
        {
            keys.Append(authKeyId);
        }
        TLDto.SecretChatState.TLObjectBuilder builder = TLDto.SecretChatState.Builder()
            .ChatId(chatId)
            .AccessHash(accessHash)
            .InitiatorUserId(initiatorUserId)
            .RecipientUserId(recipientUserId)
            .InitiatorAuthKeyId(initiatorAuthKeyId)
            .State((int)state)
            .CreatedAt(createdAt)
            .UpdatedAt(updatedAt)
            .GA(gA)
            .InitiatorReadMaxDate(initiatorReadMaxDate)
            .RecipientReadMaxDate(recipientReadMaxDate)
            .RequestedRecipientAuthKeyIds(keys);
        if (historyDeleted)
        {
            builder = builder.HistoryDeleted(true);
        }
        if (recipientAuthKeyId is long recipientKey)
        {
            builder = builder.RecipientAuthKeyId(recipientKey);
        }
        if (gB is not null)
        {
            builder = builder.GB(gB);
        }
        if (keyFingerprint is long fingerprint)
        {
            builder = builder.KeyFingerprint(fingerprint);
        }
        return builder.Build();
    }

    private static TLDto.TLSecretChatRequestedDevice BuildRequestedDevice(
        long authKeyId, int chatId, long userId, int date)
    {
        return TLDto.SecretChatRequestedDevice.Builder()
            .AuthKeyId(authKeyId)
            .ChatId(chatId)
            .UserId(userId)
            .Date(date)
            .Build();
    }

    private static TLDto.TLSecretChatControlTarget BuildControlTarget(
        long authKeyId, int chatId, long userId, int date) =>
        TLDto.SecretChatControlTarget.Builder()
            .AuthKeyId(authKeyId)
            .ChatId(chatId)
            .UserId(userId)
            .Date(date)
            .Build();

    private static TLDto.TLSecretChatPendingPair BuildPendingPair(
        TLDto.TLSecretChatState chat)
    {
        TLDto.SecretChatState row = chat.AsSecretChatState();
        return TLDto.SecretChatPendingPair.Builder()
            .InitiatorUserId(row.InitiatorUserId)
            .RecipientUserId(row.RecipientUserId)
            .ChatId(row.ChatId)
            .Build();
    }

    private static TLDto.TLSecretChatQtsPending BuildQtsPending(int chatId,
        long randomId, int date, int expiresAt, ReadOnlySpan<byte> encryptedMessage)
    {
        return TLDto.SecretChatQtsPending.Builder()
            .ChatId(chatId)
            .RandomId(randomId)
            .Date(date)
            .ExpiresAt(expiresAt)
            .EncryptedMessage(encryptedMessage)
            .Build();
    }

    private static TLDto.TLSecretChatQtsState BuildQtsState(long authKeyId,
        int lastPersistedQts, int acknowledgedQts, int queuedEvents, long queuedBytes,
        TLDto.TLSecretChatQtsPending? pending)
        => BuildQtsState(authKeyId, lastPersistedQts, acknowledgedQts, queuedEvents,
            queuedBytes, pending, null);

    private static TLDto.TLSecretChatQtsState BuildQtsState(long authKeyId,
        int lastPersistedQts, int acknowledgedQts, int queuedEvents, long queuedBytes,
        TLDto.TLSecretChatQtsPending? pending,
        TLDto.TLSecretChatSendQtsPending? pendingSend)
    {
        TLDto.SecretChatQtsState.TLObjectBuilder builder =
            TLDto.SecretChatQtsState.Builder()
                .AuthKeyId(authKeyId)
                .LastPersistedQts(lastPersistedQts)
                .AcknowledgedQts(acknowledgedQts)
                .QueuedEvents(queuedEvents)
                .QueuedBytes(queuedBytes);
        if (pending is not null)
        {
            builder = builder.PendingAppend(pending.Value.AsSpan());
        }
        if (pendingSend is not null)
        {
            builder = builder.PendingSend(pendingSend.Value.AsSpan());
        }
        return builder.Build();
    }

    private static TLDto.TLSecretChatSendQtsPending BuildSendQtsPending(
        long senderAuthKeyId, int chatId, long randomId, int date, int expiresAt,
        ReadOnlySpan<byte> encryptedMessage, ReadOnlySpan<byte> result)
    {
        return TLDto.SecretChatSendQtsPending.Builder()
            .SenderAuthKeyId(senderAuthKeyId)
            .ChatId(chatId)
            .RandomId(randomId)
            .Date(date)
            .ExpiresAt(expiresAt)
            .EncryptedMessage(encryptedMessage)
            .Result(result)
            .Build();
    }

    private static TLDto.TLSecretChatQtsEntry BuildQtsEntry(long recipientAuthKeyId,
        int qts, int chatId, long randomId, int date, int expiresAt,
        ReadOnlySpan<byte> encryptedMessage)
    {
        return TLDto.SecretChatQtsEntry.Builder()
            .RecipientAuthKeyId(recipientAuthKeyId)
            .Qts(qts)
            .ChatId(chatId)
            .RandomId(randomId)
            .Date(date)
            .ExpiresAt(expiresAt)
            .EncryptedMessage(encryptedMessage)
            .Build();
    }

    private static TLDto.TLSecretChatSendReceipt BuildSendReceipt(int chatId,
        long senderAuthKeyId, long randomId, int date, int recipientQts,
        ReadOnlySpan<byte> result)
    {
        return TLDto.SecretChatSendReceipt.Builder()
            .ChatId(chatId)
            .SenderAuthKeyId(senderAuthKeyId)
            .RandomId(randomId)
            .Date(date)
            .RecipientQts(recipientQts)
            .Result(result)
            .Build();
    }

    private static TLDto.TLSecretChatRevokedPeer BuildRevokedPeer(int chatId,
        long peerAuthKeyId, long peerUserId, long controlUpdateId)
    {
        return TLDto.SecretChatRevokedPeer.Builder()
            .ChatId(chatId)
            .PeerAuthKeyId(peerAuthKeyId)
            .PeerUserId(peerUserId)
            .ControlUpdateId(controlUpdateId)
            .Build();
    }

    private static TLDto.TLSecretChatControlUpdate BuildControlUpdate(
        TLDto.TLSecretChatControlUpdate source, int offeredStateDate)
    {
        TLDto.SecretChatControlUpdate row = source.AsSecretChatControlUpdate();
        return TLDto.SecretChatControlUpdate.Builder()
            .OfferedStateDate(offeredStateDate)
            .RecipientAuthKeyId(row.RecipientAuthKeyId)
            .UpdateId(row.UpdateId)
            .ChatId(row.ChatId)
            .PeerAuthKeyId(row.PeerAuthKeyId)
            .PeerUserId(row.PeerUserId)
            .Date(row.Date)
            .ExpiresAt(row.ExpiresAt)
            .Update(row.Update)
            .Build();
    }

    private static TLDto.TLSecretChatAuthKeyRevocation BuildRevocation(long authKeyId,
        int date, bool notificationsCompleted,
        IReadOnlyList<TLDto.TLSecretChatRevokedPeer> affectedPeers)
    {
        var peers = new Vector();
        foreach (TLDto.TLSecretChatRevokedPeer peer in affectedPeers)
        {
            peers.AppendTLObject(peer.AsSpan());
        }
        TLDto.SecretChatAuthKeyRevocation.TLObjectBuilder builder =
            TLDto.SecretChatAuthKeyRevocation.Builder()
                .AuthKeyId(authKeyId)
                .Date(date)
                .AffectedPeers(peers);
        if (notificationsCompleted)
        {
            builder = builder.NotificationsCompleted(true);
        }
        return builder.Build();
    }

    private static IReadOnlyList<TLDto.TLSecretChatRevokedPeer> AffectedPeers(
        TLDto.TLSecretChatAuthKeyRevocation revocation)
    {
        var result = new List<TLDto.TLSecretChatRevokedPeer>();
        Vector peers = revocation.AsSecretChatAuthKeyRevocation().AffectedPeers;
        for (int i = 0; i < peers.Count; i++)
        {
            byte[] bytes = peers.ReadTLObject().ToArray();
            result.Add(new TLDto.TLSecretChatRevokedPeer(bytes, 0, bytes.Length));
        }
        return result;
    }

    private static TLDto.TLSecretChatQtsPending? GetPendingAppend(
        TLDto.TLSecretChatQtsState state)
    {
        TLDto.SecretChatQtsState row = state.AsSecretChatQtsState();
        return row.Flags[0] ? row.Get_PendingAppend() : null;
    }

    private static TLDto.TLSecretChatSendQtsPending? GetPendingSend(
        TLDto.TLSecretChatQtsState state)
    {
        TLDto.SecretChatQtsState row = state.AsSecretChatQtsState();
        return row.Flags[1] ? row.Get_PendingSend() : null;
    }

    private static bool IsIdempotentCreate(TLDto.TLSecretChatState left,
        TLDto.TLSecretChatState right)
    {
        TLDto.SecretChatState leftRow = left.AsSecretChatState();
        TLDto.SecretChatState rightRow = right.AsSecretChatState();
        return leftRow.InitiatorUserId == rightRow.InitiatorUserId &&
               leftRow.InitiatorAuthKeyId == rightRow.InitiatorAuthKeyId &&
               leftRow.RecipientUserId == rightRow.RecipientUserId &&
               leftRow.GA.SequenceEqual(rightRow.GA);
    }

    private static bool ControlUpdatesEqual(TLDto.TLSecretChatControlUpdate left,
        TLDto.TLSecretChatControlUpdate right)
    {
        TLDto.SecretChatControlUpdate leftRow = left.AsSecretChatControlUpdate();
        TLDto.SecretChatControlUpdate rightRow = right.AsSecretChatControlUpdate();
        return leftRow.ChatId == rightRow.ChatId &&
               leftRow.PeerAuthKeyId == rightRow.PeerAuthKeyId &&
               leftRow.PeerUserId == rightRow.PeerUserId &&
               leftRow.Date == rightRow.Date &&
               leftRow.ExpiresAt == rightRow.ExpiresAt &&
               leftRow.Update.SequenceEqual(rightRow.Update);
    }

    private static int ChatId(TLDto.TLSecretChatState chat) =>
        chat.AsSecretChatState().ChatId;

    private static long InitiatorAuthKeyId(TLDto.TLSecretChatState chat) =>
        chat.AsSecretChatState().InitiatorAuthKeyId;

    private static long InitiatorUserId(TLDto.TLSecretChatState chat) =>
        chat.AsSecretChatState().InitiatorUserId;

    private static long RecipientUserId(TLDto.TLSecretChatState chat) =>
        chat.AsSecretChatState().RecipientUserId;

    private static int CreatedAt(TLDto.TLSecretChatState chat) =>
        chat.AsSecretChatState().CreatedAt;

    private static SecretChatPersistenceState State(TLDto.TLSecretChatState chat) =>
        (SecretChatPersistenceState)chat.AsSecretChatState().State;

    private static long? RecipientAuthKeyId(TLDto.TLSecretChatState chat)
    {
        TLDto.SecretChatState row = chat.AsSecretChatState();
        return row.Flags[1] ? row.RecipientAuthKeyId : null;
    }

    private static long[] RequestedRecipientAuthKeyIds(
        TLDto.TLSecretChatState chat) =>
        chat.AsSecretChatState().RequestedRecipientAuthKeyIds.ToArray();

    private static TLDto.TLSecretChatState ReadChat(byte[] bytes) =>
        new(bytes, 0, bytes.Length);

    private static TLDto.TLSecretChatRequestedDevice ReadRequestedDevice(byte[] bytes) =>
        new(bytes, 0, bytes.Length);

    private static TLDto.TLSecretChatQtsEntry ReadQtsEntry(byte[] bytes) =>
        new(bytes, 0, bytes.Length);

    private static TLDto.TLSecretChatQtsState ReadQtsState(byte[] bytes) =>
        new(bytes, 0, bytes.Length);

    private static TLDto.TLSecretChatSendReceipt ReadSendReceipt(byte[] bytes) =>
        new(bytes, 0, bytes.Length);

    private static TLDto.TLSecretChatEncryptedFile ReadEncryptedFile(byte[] bytes) =>
        new(bytes, 0, bytes.Length);

    private static TLDto.TLSecretChatEncryptedFileAssociation
        ReadEncryptedFileAssociation(byte[] bytes) => new(bytes, 0, bytes.Length);

    private static TLDto.TLSecretChatControlUpdate ReadControlUpdate(byte[] bytes) =>
        new(bytes, 0, bytes.Length);

    private static TLDto.TLSecretChatControlTarget ReadControlTarget(byte[] bytes) =>
        new(bytes, 0, bytes.Length);

    private static TLDto.TLSecretChatAuthKeyRevocation ReadRevocation(byte[] bytes) =>
        new(bytes, 0, bytes.Length);

    private static SemaphoreSlim[] CreateGates() =>
        Enumerable.Range(0, StripeCount).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    private async ValueTask<IReadOnlyList<SemaphoreSlim>> AcquireAuthKeyGatesAsync(
        IEnumerable<long> authKeyIds, CancellationToken cancellationToken)
    {
        SemaphoreSlim[] gates = authKeyIds
            .Select(GetAuthKeyGateIndex)
            .Distinct()
            .Order()
            .Select(index => _authKeyGates[index])
            .ToArray();
        int acquired = 0;
        try
        {
            for (; acquired < gates.Length; acquired++)
            {
                await gates[acquired].WaitAsync(cancellationToken);
            }
            return gates;
        }
        catch
        {
            for (int i = acquired - 1; i >= 0; i--)
            {
                gates[i].Release();
            }
            throw;
        }
    }

    private static void ReleaseGates(IReadOnlyList<SemaphoreSlim> gates)
    {
        for (int i = gates.Count - 1; i >= 0; i--)
        {
            gates[i].Release();
        }
    }

    private static int GetAuthKeyGateIndex(long authKeyId) =>
        (int)(unchecked((ulong)authKeyId) % StripeCount);

    private static SemaphoreSlim GetGate(SemaphoreSlim[] gates, long key)
    {
        ulong positive = unchecked((ulong)key);
        return gates[(int)(positive % (uint)gates.Length)];
    }

    private static long CreateControlUpdateId()
    {
        long id;
        do
        {
            id = BitConverter.ToInt64(RandomNumberGenerator.GetBytes(sizeof(long))) &
                 long.MaxValue;
        } while (id == 0);
        return id;
    }

    private async ValueTask FlushAsync(string operation)
    {
        if (!await _flush())
        {
            throw new IOException($"Failed to persist {operation}.");
        }
    }

}
