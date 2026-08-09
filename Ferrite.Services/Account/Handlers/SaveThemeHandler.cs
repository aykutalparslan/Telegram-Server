// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class SaveThemeHandler : ThemeHandlerBase
{
    public SaveThemeHandler(ThemeStore store, ProfileStore profiles)
        : base(store, profiles) { }

    [TLFunction(Constructors.baseLayer_SaveTheme)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new SaveTheme(q.AsSpan());
        return TryReadTheme(request.Get_ThemeView(), out ThemeInput input)
            ? await Store.SaveAsync(userId.Value, input, request.Unsave)
            : Invalid();
    }
}
