// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class SetAuthorizationTTLHandler : AccountSettingsHandlerBase
{
    public SetAuthorizationTTLHandler(AccountSettingsStore store) : base(store) { }

    [TLFunction(Constructors.baseLayer_SetAuthorizationTTL)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        int days = new SetAuthorizationTTL(q.AsSpan()).AuthorizationTtlDays;
        return await Store.SetAuthorizationTtlAsync(userId.Value, days);
    }
}

