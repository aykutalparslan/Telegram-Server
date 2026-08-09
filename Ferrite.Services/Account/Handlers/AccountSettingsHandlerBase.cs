// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;

namespace Ferrite.Services.Handlers.AccountMethods;

public abstract class AccountSettingsHandlerBase
{
    protected readonly AccountSettingsStore Store;

    protected AccountSettingsHandlerBase(AccountSettingsStore store) =>
        Store = store;

    protected ValueTask<long?> GetUserIdAsync(long authKeyId) =>
        Store.GetUserIdAsync(authKeyId);

    protected static TLBytes AuthError() =>
        RpcErrorGenerator.GenerateError(401, "AUTH_KEY_INVALID"u8);

    protected static TLBytes Invalid(string message) =>
        RpcErrorGenerator.GenerateError(400,
            System.Text.Encoding.UTF8.GetBytes(message));
}

