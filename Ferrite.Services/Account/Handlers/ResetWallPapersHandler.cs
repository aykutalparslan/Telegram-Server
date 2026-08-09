// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class ResetWallPapersHandler : WallpaperHandlerBase
{
    public ResetWallPapersHandler(WallpaperStore store, ProfileStore profiles)
        : base(store, profiles) { }

    [TLFunction(Constructors.baseLayer_ResetWallPapers)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue ? await Store.ResetAsync(userId.Value)
            : AuthError();
    }
}
