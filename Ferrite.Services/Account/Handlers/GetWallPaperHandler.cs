// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class GetWallPaperHandler : WallpaperHandlerBase
{
    public GetWallPaperHandler(WallpaperStore store, ProfileStore profiles)
        : base(store, profiles) { }

    [TLFunction(Constructors.baseLayer_GetWallPaper)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        InputWallPaperView input = new GetWallPaper(q.AsSpan())
            .Get_WallpaperView();
        return TryParse(input, out WallpaperInput value)
            ? await Store.GetAsync(userId.Value, value) : Invalid();
    }
}
