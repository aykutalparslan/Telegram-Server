// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Handlers.AccountMethods;

public abstract class WallpaperHandlerBase
{
    protected readonly WallpaperStore Store;
    private readonly ProfileStore _profiles;

    protected WallpaperHandlerBase(WallpaperStore store, ProfileStore profiles)
    {
        Store = store;
        _profiles = profiles;
    }

    protected ValueTask<long?> GetUserIdAsync(long authKeyId) =>
        _profiles.GetUserIdAsync(authKeyId);

    protected static bool TryParse(InputWallPaperView input,
        out WallpaperInput value)
    {
        if (input.Is(out InputWallPaper id))
        {
            value = new WallpaperInput(WallpaperInputKind.Id, id.Id,
                id.AccessHash, string.Empty);
            return id.Id != 0;
        }
        if (input.Is(out InputWallPaperSlug slug))
        {
            string text = Encoding.UTF8.GetString(slug.Slug).Trim();
            value = new WallpaperInput(WallpaperInputKind.Slug, 0, 0, text);
            return text.Length > 0;
        }
        if (input.Is(out InputWallPaperNoFile noFile))
        {
            value = new WallpaperInput(WallpaperInputKind.NoFile, noFile.Id,
                0, string.Empty);
            return noFile.Id >= 0;
        }
        value = default;
        return false;
    }

    protected static TLWallPaperSettings? CloneSettings(
        WallPaperSettingsView view)
    {
        if (!view.Is(out WallPaperSettings settings) ||
            !WallpaperStore.IsSettingsValid(settings)) return null;
        return settings.Clone().Build();
    }

    protected static TLBytes AuthError() =>
        RpcErrorGenerator.GenerateError(401, "AUTH_KEY_INVALID"u8);
    protected static TLBytes Invalid() =>
        RpcErrorGenerator.GenerateError(400, "WALLPAPER_INVALID"u8);
}
