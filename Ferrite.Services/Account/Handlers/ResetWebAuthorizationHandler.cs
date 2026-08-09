// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class ResetWebAuthorizationHandler : AccountSettingsHandlerBase
{
    public ResetWebAuthorizationHandler(AccountSettingsStore store) : base(store) { }

    [TLFunction(Constructors.baseLayer_ResetWebAuthorization)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q) =>
        await GetUserIdAsync(authKeyId) is { }
            ? Invalid("AUTH_ID_INVALID") : AuthError();
}

