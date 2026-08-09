// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

public sealed class SecretChatAuthKeyCleanup : ISecretChatAuthKeyCleanup
{
    private readonly ISecretChatsRepository _secretChatsRepository;

    private const int ControlUpdateRetentionSeconds = 7 * 24 * 60 * 60;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUpdatesService _updates;
    private readonly IMTProtoTime _time;

    public SecretChatAuthKeyCleanup(IUnitOfWork unitOfWork, ISecretChatsRepository secretChatsRepository, IUpdatesService updates,
        IMTProtoTime time)
    {
        _secretChatsRepository = secretChatsRepository;

        _unitOfWork = unitOfWork;
        _updates = updates;
        _time = time;
    }

    public async ValueTask CleanupAsync(long authKeyId,
        CancellationToken cancellationToken = default)
    {
        int requestedDate = checked((int)_time.GetUnixTimeInSeconds());
        SecretChatAuthKeyRevocationResult revocation = await _secretChatsRepository.RevokeAuthKeyAsync(authKeyId, requestedDate,
                cancellationToken);

        foreach (TLDto.TLSecretChatRevokedPeer peerValue in revocation.AffectedPeers)
        {
            int chatId;
            long peerAuthKeyId;
            long peerUserId;
            long controlUpdateId;
            using (peerValue)
            {
                TLDto.SecretChatRevokedPeer peer =
                    peerValue.AsSecretChatRevokedPeer();
                chatId = peer.ChatId;
                peerAuthKeyId = peer.PeerAuthKeyId;
                peerUserId = peer.PeerUserId;
                controlUpdateId = peer.ControlUpdateId;
            }

            byte[] updateBytes = BuildDiscardUpdate(chatId, revocation.Date);
            IReadOnlyList<TLDto.TLSecretChatControlUpdate> existing = await _secretChatsRepository.GetControlUpdatesAsync(peerAuthKeyId,
                    cancellationToken);
            bool wasAlreadyDurable = false;
            foreach (TLDto.TLSecretChatControlUpdate existingValue in existing)
            {
                using (existingValue)
                {
                    if (existingValue.AsSecretChatControlUpdate().UpdateId ==
                        controlUpdateId)
                    {
                        wasAlreadyDurable = true;
                    }
                }
            }

            using TLDto.TLSecretChatControlUpdate control =
                TLDto.SecretChatControlUpdate.Builder()
                    .RecipientAuthKeyId(peerAuthKeyId)
                    .UpdateId(controlUpdateId)
                    .ChatId(chatId)
                    .PeerAuthKeyId(authKeyId)
                    .PeerUserId(peerUserId)
                    .Date(revocation.Date)
                    .ExpiresAt(checked(revocation.Date +
                        ControlUpdateRetentionSeconds))
                    .Update(updateBytes)
                    .Build();
            if (!await _secretChatsRepository.PutControlUpdateAsync(control,
                    cancellationToken))
            {
                throw new IOException(
                    $"Failed to persist secret-chat revocation update {controlUpdateId}.");
            }

            if (!wasAlreadyDurable)
            {
                await _updates.EnqueueUpdate(peerUserId,
                    new TLUpdate(updateBytes, 0, updateBytes.Length),
                    UpdateDeliveryScope.ForAuthKey(peerAuthKeyId));
            }
        }

        if (!await _secretChatsRepository
                .CompleteAuthKeyRevocationAsync(authKeyId, cancellationToken))
        {
            throw new IOException(
                $"Failed to complete secret-chat cleanup for auth key {authKeyId}.");
        }
    }

    private static byte[] BuildDiscardUpdate(int chatId, int date)
    {
        using TLEncryptedChat chat = EncryptedChatDiscarded.Builder()
            .Id(chatId)
            .Build();
        using TLUpdate update = UpdateEncryption.Builder()
            .Chat(chat.AsSpan())
            .Date(date)
            .Build();
        return update.AsSpan().ToArray();
    }
}
