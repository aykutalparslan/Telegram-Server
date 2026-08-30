// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.SecretChats.Handlers;

public abstract class SecretChatLifecycleHandlerBase
{
    private readonly IBlockedPeersRepository _blockedPeersRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly ISecretChatsRepository _secretChatsRepository;

    protected readonly IUnitOfWork UnitOfWork;
    protected readonly ISecretChatDeviceSelector DeviceSelector;
    protected readonly SecretChatControlDelivery ControlDelivery;
    protected readonly SecretChatLimits Limits;
    protected readonly SecretChatTelemetry? Telemetry;

    protected SecretChatLifecycleHandlerBase(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, ISecretChatsRepository secretChatsRepository,
        ISecretChatDeviceSelector deviceSelector,
        SecretChatControlDelivery controlDelivery, SecretChatLimits limits,
        SecretChatTelemetry? telemetry = null)
    {
        _blockedPeersRepository = blockedPeersRepository;

        _authorizationRepository = authorizationRepository;
        _secretChatsRepository = secretChatsRepository;

        UnitOfWork = unitOfWork;
        DeviceSelector = deviceSelector;
        ControlDelivery = controlDelivery;
        Limits = limits;
        Telemetry = telemetry;
    }

    protected async ValueTask<long?> GetCurrentUserIdAsync(long authKeyId)
    {
        TLDto.TLAuthInfo? authorization = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (authorization is null)
        {
            return null;
        }

        using TLDto.TLAuthInfo ownedAuthorization = authorization.Value;
        TLDto.AuthInfo row = ownedAuthorization.AsAuthInfo();
        return row.LoggedIn ? row.UserId : null;
    }

    protected bool IsBlockedBy(long ownerUserId, long peerUserId)
    {
        bool blocked = false;
        foreach (TLDto.TLBlockedPeer blockedValue in _blockedPeersRepository.GetBlockedPeers(ownerUserId))
        {
            using (blockedValue)
            {
                TLDto.BlockedPeer row = blockedValue.AsBlockedPeer();
                blocked |= row.PeerType == (int)PeerType.User &&
                           row.PeerId == peerUserId;
            }
        }
        return blocked;
    }

    protected async ValueTask<bool> EnsureRequestedControlsAsync(
        TLDto.TLSecretChatState chat,
        CancellationToken cancellationToken = default)
    {
        TLDto.SecretChatState row = chat.AsSecretChatState();
        int chatId = row.ChatId;
        long accessHash = row.AccessHash;
        int date = row.CreatedAt;
        long initiatorUserId = row.InitiatorUserId;
        long recipientUserId = row.RecipientUserId;
        long initiatorAuthKeyId = row.InitiatorAuthKeyId;
        byte[] gA = row.GA.ToArray();
        long[] requestedAuthKeyIds = row.RequestedRecipientAuthKeyIds.ToArray();

        bool persisted = true;
        foreach (long recipientAuthKeyId in requestedAuthKeyIds.Distinct())
        {
            persisted &= await ControlDelivery.EnsureAsync(recipientAuthKeyId,
                recipientUserId, initiatorAuthKeyId, initiatorUserId, chatId, date,
                SecretChatControlKind.Requested,
                BuildRequested(chatId, accessHash, date, initiatorUserId,
                    recipientUserId, gA), cancellationToken);
        }
        return persisted;
    }

    protected async ValueTask<bool> EnsureAcceptedControlsAsync(
        TLDto.TLSecretChatState chat,
        CancellationToken cancellationToken = default)
    {
        TLDto.SecretChatState row = chat.AsSecretChatState();
        if (!row.Flags[1] || !row.Flags[2] || !row.Flags[3])
        {
            return false;
        }
        int chatId = row.ChatId;
        long accessHash = row.AccessHash;
        int createdAt = row.CreatedAt;
        int updatedAt = row.UpdatedAt;
        long initiatorUserId = row.InitiatorUserId;
        long recipientUserId = row.RecipientUserId;
        long initiatorAuthKeyId = row.InitiatorAuthKeyId;
        long recipientAuthKeyId = row.RecipientAuthKeyId;
        byte[] gB = row.GB.ToArray();
        long keyFingerprint = row.KeyFingerprint;
        long[] requestedAuthKeyIds = row.RequestedRecipientAuthKeyIds.ToArray();

        bool persisted = await ControlDelivery.EnsureAsync(initiatorAuthKeyId,
            initiatorUserId,
            recipientAuthKeyId, recipientUserId, chatId, updatedAt,
            SecretChatControlKind.Accepted,
            BuildActive(chatId, accessHash, createdAt, initiatorUserId,
                recipientUserId, gB, keyFingerprint), cancellationToken);

        foreach (long losingAuthKeyId in requestedAuthKeyIds
                     .Where(x => x != recipientAuthKeyId).Distinct())
        {
            persisted &= await ControlDelivery.EnsureAsync(losingAuthKeyId,
                recipientUserId,
                initiatorAuthKeyId, initiatorUserId, chatId, updatedAt,
                SecretChatControlKind.LosingDeviceDiscarded,
                BuildDiscarded(chatId, false), cancellationToken);
        }
        return persisted;
    }

    protected async ValueTask<bool> EnsureDiscardedControlsAsync(
        TLDto.TLSecretChatState chat, long callerAuthKeyId,
        IReadOnlyList<long>? notificationAuthKeyIds = null,
        CancellationToken cancellationToken = default)
    {
        TLDto.SecretChatState row = chat.AsSecretChatState();
        int chatId = row.ChatId;
        int date = row.UpdatedAt;
        bool historyDeleted = row.HistoryDeleted;
        long initiatorUserId = row.InitiatorUserId;
        long recipientUserId = row.RecipientUserId;
        long initiatorAuthKeyId = row.InitiatorAuthKeyId;
        long? recipientAuthKeyId = row.Flags[1] ? row.RecipientAuthKeyId : null;
        long[] requestedAuthKeyIds = row.RequestedRecipientAuthKeyIds.ToArray();

        IEnumerable<long> inferredTargets = recipientAuthKeyId is long activeKey
            ? new[] { initiatorAuthKeyId, activeKey }
            : requestedAuthKeyIds.Append(initiatorAuthKeyId);
        long[] targets = (notificationAuthKeyIds ?? inferredTargets.ToArray())
            .Where(x => x != callerAuthKeyId).Distinct().ToArray();

        bool persisted = true;
        foreach (long targetAuthKeyId in targets)
        {
            bool targetsInitiator = targetAuthKeyId == initiatorAuthKeyId;
            long targetUserId = targetsInitiator
                ? initiatorUserId
                : recipientUserId;
            long inferredPendingActor = requestedAuthKeyIds
                .Except(targets).FirstOrDefault();
            long peerAuthKeyId = targetsInitiator
                ? recipientAuthKeyId ?? (callerAuthKeyId != 0
                    ? callerAuthKeyId
                    : inferredPendingActor)
                : initiatorAuthKeyId;
            long peerUserId = targetsInitiator
                ? recipientUserId
                : initiatorUserId;
            persisted &= await ControlDelivery.EnsureAsync(targetAuthKeyId,
                targetUserId,
                peerAuthKeyId, peerUserId, chatId, date,
                SecretChatControlKind.Discarded,
                BuildDiscarded(chatId, historyDeleted), cancellationToken);
        }
        return persisted;
    }

    protected async ValueTask CompleteControlTransitionAsync(
        TLDto.TLSecretChatState chat, bool durable,
        CancellationToken cancellationToken = default)
    {
        if (!durable)
        {
            return;
        }
        TLDto.SecretChatState row = chat.AsSecretChatState();
        await _secretChatsRepository.CompleteControlTransitionAsync(
            row.ChatId, (SecretChatPersistenceState)row.State, row.UpdatedAt,
            cancellationToken);
    }

    protected ValueTask<IReadOnlyList<long>> GetControlTargetAuthKeyIdsAsync(
        TLDto.TLSecretChatState chat,
        CancellationToken cancellationToken = default) =>
        _secretChatsRepository.GetControlTargetAuthKeyIdsAsync(
            chat.AsSecretChatState().ChatId, cancellationToken);

    protected static TLEncryptedChat BuildWaiting(int chatId, long accessHash,
        int date, long initiatorUserId, long recipientUserId) =>
        EncryptedChatWaiting.Builder()
            .Id(chatId)
            .AccessHash(accessHash)
            .Date(date)
            .AdminId(initiatorUserId)
            .ParticipantId(recipientUserId)
            .Build();

    protected static TLEncryptedChat BuildRequested(int chatId, long accessHash,
        int date, long initiatorUserId, long recipientUserId, ReadOnlySpan<byte> gA) =>
        EncryptedChatRequested.Builder()
            .Id(chatId)
            .AccessHash(accessHash)
            .Date(date)
            .AdminId(initiatorUserId)
            .ParticipantId(recipientUserId)
            .GA(gA)
            .Build();

    protected static TLEncryptedChat BuildActive(int chatId, long accessHash,
        int date, long initiatorUserId, long recipientUserId,
        ReadOnlySpan<byte> gAOrB, long keyFingerprint) =>
        EncryptedChat.Builder()
            .Id(chatId)
            .AccessHash(accessHash)
            .Date(date)
            .AdminId(initiatorUserId)
            .ParticipantId(recipientUserId)
            .GAOrB(gAOrB)
            .KeyFingerprint(keyFingerprint)
            .Build();

    protected static TLEncryptedChat BuildDiscarded(int chatId,
        bool historyDeleted)
    {
        EncryptedChatDiscarded.TLObjectBuilder builder =
            EncryptedChatDiscarded.Builder().Id(chatId);
        if (historyDeleted)
        {
            builder = builder.HistoryDeleted(true);
        }
        return builder.Build();
    }
}
