// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class FinishTakeoutSessionHandler : AccountSettingsHandlerBase
{
    public FinishTakeoutSessionHandler(AccountSettingsStore store) : base(store) { }

    [TLFunction(Constructors.baseLayer_FinishTakeoutSession)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        if (await GetUserIdAsync(authKeyId) is null) return AuthError();
        return await Store.FinishTakeoutAsync(authKeyId,
            new FinishTakeoutSession(q.AsSpan()).Success);
    }
}
