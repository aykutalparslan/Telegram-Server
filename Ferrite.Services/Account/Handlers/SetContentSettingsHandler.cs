// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class SetContentSettingsHandler : AccountSettingsHandlerBase
{
    public SetContentSettingsHandler(AccountSettingsStore store) : base(store) { }

    [TLFunction(Constructors.baseLayer_SetContentSettings)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        bool enabled = new SetContentSettings(q.AsSpan()).SensitiveEnabled;
        return await Store.SetContentSettingsAsync(userId.Value, enabled);
    }
}

