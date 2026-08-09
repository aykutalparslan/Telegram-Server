// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class GetContentSettingsHandler
{
    private readonly AccountSettingsStore _store;

    public GetContentSettingsHandler(AccountSettingsStore store) => _store = store;

    [TLFunction(Constructors.baseLayer_GetContentSettings)]
    public async ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await _store.GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await _store.GetContentSettingsAsync(userId.Value)
            : RpcErrorGenerator.GenerateError(401, "AUTH_KEY_INVALID"u8);
    }
}
