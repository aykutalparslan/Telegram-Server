// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.SecretChats;

public interface ISecretChatQtsQueue
{
    ValueTask<SecretChatQtsAppendResult> EnqueueAsync(long recipientAuthKeyId,
        long recipientUserId, int chatId, long randomId, int date,
        TLEncryptedMessage message, CancellationToken cancellationToken = default);

    ValueTask<SecretChatSendAppendResult> EnqueueSendAsync(long senderAuthKeyId,
        long recipientAuthKeyId, long recipientUserId, int chatId, long accessHash,
        long randomId, int date, TLEncryptedMessage message,
        ReadOnlyMemory<byte> result, CancellationToken cancellationToken = default);
}
