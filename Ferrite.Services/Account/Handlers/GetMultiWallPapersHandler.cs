// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class GetMultiWallPapersHandler : WallpaperHandlerBase
{
    public GetMultiWallPapersHandler(WallpaperStore store, ProfileStore profiles)
        : base(store, profiles) { }

    [TLFunction(Constructors.baseLayer_GetMultiWallPapers)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        Vector vector = new GetMultiWallPapers(q.AsSpan()).Wallpapers;
        var values = new List<WallpaperInput>(vector.Count);
        for (int i = 0; i < vector.Count; i++)
        {
            if (!TryParse((InputWallPaperView)vector.ReadTLObject(),
                    out WallpaperInput value)) return Invalid();
            values.Add(value);
        }
        return await Store.GetMultiAsync(userId.Value, values);
    }
}
