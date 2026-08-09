// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class GetDefaultEmojiStatusesHandler : EmojiCatalogueHandlerBase
{
    public GetDefaultEmojiStatusesHandler(StickerStore stickers,
        ProfileStore profiles) : base(stickers, profiles) { }

    [TLFunction(Constructors.baseLayer_GetDefaultEmojiStatuses)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        if (!(await GetUserIdAsync(authKeyId)).HasValue) return AuthError();
        return await Stickers.GetEmojiStatusCatalogueAsync(
            new GetDefaultEmojiStatuses(q.AsSpan()).Hash);
    }
}
