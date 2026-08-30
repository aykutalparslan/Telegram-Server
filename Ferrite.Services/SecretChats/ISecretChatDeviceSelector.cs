// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.SecretChats;

public interface ISecretChatDeviceSelector
{
    ValueTask<IReadOnlyList<long>> GetEligibleAuthKeyIds(long userId);
}
