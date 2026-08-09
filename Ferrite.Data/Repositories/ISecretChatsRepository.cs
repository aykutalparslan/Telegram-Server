// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface ISecretChatsRepository
{
    ValueTask<SecretChatCreateResult> TryCreateChatAsync(TLDto.TLSecretChatState chat,
        int maxPendingChatsPerAuthKey = int.MaxValue,
        int maxOutstandingRequestsPerAuthKey = int.MaxValue,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLSecretChatState?> GetChatAsync(int chatId,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLSecretChatState?> GetChatByInitiatorRandomIdAsync(
        long initiatorUserId, int randomId,
        CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<TLDto.TLSecretChatState>> GetChatsByAuthKeyAsync(
        long authKeyId, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<long>> GetRequestedRecipientAuthKeysAsync(int chatId,
        CancellationToken cancellationToken = default);
    ValueTask<SecretChatAcceptResult> TryAcceptChatAsync(int chatId,
        long recipientAuthKeyId, ReadOnlyMemory<byte> gB, long keyFingerprint, int date,
        int maxActiveChatsPerAuthKey = int.MaxValue,
        CancellationToken cancellationToken = default);
    ValueTask<SecretChatDiscardResult> TryDiscardChatAsync(int chatId,
        long callerAuthKeyId, bool deleteHistory, int date,
        CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<TLDto.TLSecretChatState>> GetControlTargetChatsAsync(
        long authKeyId, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<long>> GetControlTargetAuthKeyIdsAsync(int chatId,
        CancellationToken cancellationToken = default);
    ValueTask<bool> CompleteControlTransitionAsync(int chatId,
        SecretChatPersistenceState expectedState, int expectedDate,
        CancellationToken cancellationToken = default);

    ValueTask<SecretChatReceiptPutResult> TryPutSendReceiptAsync(
        TLDto.TLSecretChatSendReceipt receipt,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLSecretChatSendReceipt?> GetSendReceiptAsync(int chatId,
        long senderAuthKeyId, long randomId,
        int minimumDate = int.MinValue,
        CancellationToken cancellationToken = default);
    ValueTask<SecretChatReadAdvanceStatus> AdvanceReadDateAsync(
        long callerAuthKeyId, int chatId, long accessHash, int maxDate,
        TLDto.TLSecretChatControlUpdate controlUpdate,
        CancellationToken cancellationToken = default);

    ValueTask<SecretChatQtsAppendResult> AppendQtsAsync(long recipientAuthKeyId,
        int chatId, long randomId, int date, int expiresAt,
        ReadOnlyMemory<byte> encryptedMessage, int maxEvents, long maxBytes,
        Func<ValueTask<int>> getCurrentQts, Func<ValueTask<int>> incrementQts,
        CancellationToken cancellationToken = default);
    ValueTask<SecretChatSendAppendResult> AppendSendQtsAsync(long senderAuthKeyId,
        long recipientAuthKeyId, int chatId, long accessHash, long randomId,
        int date, int expiresAt, ReadOnlyMemory<byte> encryptedMessage,
        ReadOnlyMemory<byte> result, int maxEvents, long maxBytes,
        int receiptRetentionSeconds,
        Func<ValueTask<int>> getCurrentQts, Func<ValueTask<int>> incrementQts,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLSecretChatQtsState> RecoverPendingQtsAsync(long recipientAuthKeyId,
        Func<ValueTask<int>> getCurrentQts, Func<ValueTask<int>> incrementQts,
        CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<TLDto.TLSecretChatQtsEntry>> GetQtsEntriesAsync(
        long recipientAuthKeyId, int afterQts = 0, int limit = int.MaxValue,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLSecretChatQtsState> AcknowledgeQtsAsync(long recipientAuthKeyId,
        int maxQts, CancellationToken cancellationToken = default);
    ValueTask<SecretChatQtsConfirmResult> ConfirmQtsAsync(long recipientAuthKeyId,
        int maxQts, Func<ValueTask<int>> getCurrentQts,
        Func<ValueTask<int>> incrementQts,
        CancellationToken cancellationToken = default);
    ValueTask<SecretChatQtsDifferenceResult> ReadQtsDifferenceAsync(
        long recipientAuthKeyId, int requestQts, int now, int limit,
        Func<ValueTask<int>> getCurrentQts, Func<ValueTask<int>> incrementQts,
        CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<long>> GetQtsMaintenanceAuthKeyIdsAsync(
        long afterAuthKeyId, int limit,
        CancellationToken cancellationToken = default);
    ValueTask<SecretChatQtsMaintenanceResult> MaintainQtsAsync(
        long recipientAuthKeyId, int now,
        Func<ValueTask<int>> getCurrentQts, Func<ValueTask<int>> incrementQts,
        CancellationToken cancellationToken = default);
    ValueTask<SecretChatRetentionCleanupResult> CleanupRetentionAsync(
        int now, int receiptRetentionSeconds, int maxItems,
        CancellationToken cancellationToken = default);

    ValueTask<bool> PutControlUpdateAsync(TLDto.TLSecretChatControlUpdate update,
        CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<TLDto.TLSecretChatControlUpdate>> GetControlUpdatesAsync(
        long recipientAuthKeyId, CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteControlUpdatesAsync(long recipientAuthKeyId,
        CancellationToken cancellationToken = default);
    ValueTask<SecretChatControlDifferenceResult> GetControlDifferenceAsync(
        long recipientAuthKeyId, int requestDate, int responseDate, int now,
        bool isProbe, CancellationToken cancellationToken = default);

    ValueTask<bool> PutEncryptedFileAsync(TLDto.TLSecretChatEncryptedFile file,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLSecretChatEncryptedFile?> GetEncryptedFileAsync(long fileId,
        long accessHash, CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLSecretChatEncryptedFile?> GetEncryptedFileByIdAsync(long fileId,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLSecretChatEncryptedFile?> GetEncryptedFileByUploadIdAsync(
        long uploadFileId, CancellationToken cancellationToken = default);
    ValueTask<SecretChatFileAssociationStatus> TryAssociateEncryptedFileAsync(
        TLDto.TLSecretChatEncryptedFileAssociation association, int maxAssociations,
        CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<TLDto.TLSecretChatEncryptedFileAssociation>>
        GetEncryptedFileAssociationsAsync(long fileId,
            CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<TLDto.TLSecretChatEncryptedFileAssociation>>
        GetEncryptedFilesByChatAsync(int chatId,
            CancellationToken cancellationToken = default);

    ValueTask<SecretChatAuthKeyRevocationResult> RevokeAuthKeyAsync(long authKeyId,
        int date, CancellationToken cancellationToken = default);
    ValueTask<bool> CompleteAuthKeyRevocationAsync(long authKeyId,
        CancellationToken cancellationToken = default);
}
