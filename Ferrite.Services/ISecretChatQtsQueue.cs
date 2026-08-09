// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services;

public interface ISecretChatQtsQueue
{
    /// <summary>
    /// Consumes <paramref name="message"/>, persists it for the recipient
    /// authorization, and attempts exact-device live delivery. The caller owns
    /// any qts entry returned in the result.
    /// </summary>
    ValueTask<SecretChatQtsAppendResult> EnqueueAsync(long recipientAuthKeyId,
        long recipientUserId, int chatId, long randomId, int date,
        TLEncryptedMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes <paramref name="message"/> and atomically persists its qts entry
    /// with the sender's idempotency receipt before exact-device live delivery.
    /// The caller owns any entry and receipt returned in the result.
    /// </summary>
    ValueTask<SecretChatSendAppendResult> EnqueueSendAsync(long senderAuthKeyId,
        long recipientAuthKeyId, long recipientUserId, int chatId, long accessHash,
        long randomId, int date, TLEncryptedMessage message,
        ReadOnlyMemory<byte> result, CancellationToken cancellationToken = default);
}
