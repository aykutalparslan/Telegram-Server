// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class GetReactionsNotifySettingsHandler
{
    private readonly AccountSettingsStore? _store;

    public GetReactionsNotifySettingsHandler(AccountSettingsStore? store = null) =>
        _store = store;

    [TLFunction(Constructors.baseLayer_GetReactionsNotifySettings)]
    public async ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        if (_store is not null)
        {
            long? userId = await _store.GetUserIdAsync(authKeyId);
            return userId.HasValue
                ? await _store.GetReactionsNotifySettingsAsync(userId.Value)
                : RpcErrorGenerator.GenerateError(401, "AUTH_KEY_INVALID"u8);
        }
        using var sound = NotificationSoundDefault.Builder().Build();
        var result = ReactionsNotifySettings.Builder()
            .Sound(sound.ToReadOnlySpan())
            .ShowPreviews(true)
            .Build();
        return result.TLBytes!.Value;
    }
}
