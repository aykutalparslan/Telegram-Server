// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class ClearRecentEmojiStatusesHandler : EmojiCatalogueHandlerBase
{
    public ClearRecentEmojiStatusesHandler(EmojiCatalogStore stickers,
        ProfileStore profiles) : base(stickers, profiles) { }

    [TLFunction(Constructors.baseLayer_ClearRecentEmojiStatuses)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await Profiles.ClearRecentEmojiStatusesAsync(userId.Value)
            : AuthError();
    }
}
