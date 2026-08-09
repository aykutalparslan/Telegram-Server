// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class GetThemeHandler : ThemeHandlerBase
{
    public GetThemeHandler(ThemeStore store, ProfileStore profiles)
        : base(store, profiles) { }

    [TLFunction(Constructors.baseLayer_GetTheme)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new GetTheme(q.AsSpan());
        return TryReadTheme(request.Get_ThemeView(), out ThemeInput input)
            ? await Store.GetAsync(userId.Value,
                Encoding.UTF8.GetString(request.Format).Trim(), input)
            : Invalid();
    }
}
