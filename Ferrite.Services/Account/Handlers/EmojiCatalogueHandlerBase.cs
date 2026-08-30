// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;

namespace Ferrite.Services.Handlers.AccountMethods;

public abstract class EmojiCatalogueHandlerBase
{
    protected readonly EmojiCatalogStore Stickers;
    protected readonly ProfileStore Profiles;

    protected EmojiCatalogueHandlerBase(EmojiCatalogStore stickers,
        ProfileStore profiles)
    {
        Stickers = stickers;
        Profiles = profiles;
    }

    protected ValueTask<long?> GetUserIdAsync(long authKeyId) =>
        Profiles.GetUserIdAsync(authKeyId);

    protected static TLBytes AuthError() =>
        RpcErrorGenerator.GenerateError(401, "AUTH_KEY_INVALID"u8);
}
