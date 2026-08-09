// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.SecretChats.Handlers;

public interface ISecretChatTransitionRepair
{
    ValueTask RepairAsync(long authKeyId,
        CancellationToken cancellationToken = default);
}
