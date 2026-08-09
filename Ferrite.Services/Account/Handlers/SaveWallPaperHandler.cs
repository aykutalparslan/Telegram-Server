// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class SaveWallPaperHandler : WallpaperHandlerBase
{
    public SaveWallPaperHandler(WallpaperStore store, ProfileStore profiles)
        : base(store, profiles) { }

    [TLFunction(Constructors.baseLayer_SaveWallPaper)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new SaveWallPaper(q.AsSpan());
        if (!TryParse(request.Get_WallpaperView(), out WallpaperInput input))
            return Invalid();
        using TLWallPaperSettings? settings = CloneSettings(
            request.Get_SettingsView());
        if (settings is null) return Invalid();
        return await Store.SaveAsync(userId.Value, input, request.Unsave,
            settings.Value);
    }
}
