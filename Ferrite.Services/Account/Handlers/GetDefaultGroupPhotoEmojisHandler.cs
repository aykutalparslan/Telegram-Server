// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class GetDefaultGroupPhotoEmojisHandler : EmojiCatalogueHandlerBase
{
    public GetDefaultGroupPhotoEmojisHandler(EmojiCatalogStore stickers,
        ProfileStore profiles) : base(stickers, profiles) { }

    [TLFunction(Constructors.baseLayer_GetDefaultGroupPhotoEmojis)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        if (!(await GetUserIdAsync(authKeyId)).HasValue) return AuthError();
        return await Stickers.GetEmojiIdCatalogueAsync(
            new GetDefaultGroupPhotoEmojis(q.AsSpan()).Hash);
    }
}
