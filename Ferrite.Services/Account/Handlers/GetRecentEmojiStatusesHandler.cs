// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class GetRecentEmojiStatusesHandler : EmojiCatalogueHandlerBase
{
    public GetRecentEmojiStatusesHandler(StickerStore stickers,
        ProfileStore profiles) : base(stickers, profiles) { }

    [TLFunction(Constructors.baseLayer_GetRecentEmojiStatuses)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        return await Profiles.GetRecentEmojiStatusesAsync(userId.Value,
            new GetRecentEmojiStatuses(q.AsSpan()).Hash);
    }
}
