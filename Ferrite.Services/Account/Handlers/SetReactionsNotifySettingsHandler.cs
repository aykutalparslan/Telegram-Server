// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class SetReactionsNotifySettingsHandler : AccountSettingsHandlerBase
{
    public SetReactionsNotifySettingsHandler(AccountSettingsStore store) : base(store) { }

    [TLFunction(Constructors.baseLayer_SetReactionsNotifySettings)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new SetReactionsNotifySettings(q.AsSpan());
        using TLReactionsNotifySettings settings = request.Get_SettingsView()
            .AsReactionsNotifySettings().Clone().Build();
        return await Store.SetReactionsNotifySettingsAsync(userId.Value, settings);
    }
}
