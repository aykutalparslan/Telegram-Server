// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class SetGlobalPrivacySettingsHandler : AccountSettingsHandlerBase
{
    public SetGlobalPrivacySettingsHandler(AccountSettingsStore store) : base(store) { }

    [TLFunction(Constructors.baseLayer_SetGlobalPrivacySettings)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new SetGlobalPrivacySettings(q.AsSpan());
        using TLGlobalPrivacySettings settings = request.Get_SettingsView()
            .AsGlobalPrivacySettings().Clone().Build();
        return await Store.SetGlobalPrivacyAsync(userId.Value, settings);
    }
}
