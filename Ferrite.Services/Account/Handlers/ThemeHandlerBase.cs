// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Handlers.AccountMethods;

public abstract class ThemeHandlerBase
{
    protected readonly ThemeStore Store;
    private readonly ProfileStore _profiles;

    protected ThemeHandlerBase(ThemeStore store, ProfileStore profiles)
    {
        Store = store;
        _profiles = profiles;
    }

    protected ValueTask<long?> GetUserIdAsync(long authKeyId) =>
        _profiles.GetUserIdAsync(authKeyId);

    protected static bool TryReadTheme(InputThemeView view,
        out ThemeInput input)
    {
        if (view.Is(out InputTheme id) && id.Id > 0)
        {
            input = new ThemeInput(ThemeInputKind.Id, id.Id, id.AccessHash,
                string.Empty);
            return true;
        }
        if (view.Is(out InputThemeSlug slug))
        {
            string value = Encoding.UTF8.GetString(slug.Slug).Trim();
            input = new ThemeInput(ThemeInputKind.Slug, 0, 0, value);
            return value.Length > 0;
        }
        input = default;
        return false;
    }

    protected static bool TryReadDocument(InputDocumentView view,
        out ThemeDocumentInput input)
    {
        if (view.Is(out InputDocument document) && document.Id > 0)
        {
            input = new ThemeDocumentInput(document.Id, document.AccessHash,
                document.FileReference.ToArray());
            return true;
        }
        input = default;
        return false;
    }

    protected static bool TryReadSettings(Vector vector,
        out List<ThemeSettingsInput> result)
    {
        result = new List<ThemeSettingsInput>(vector.Count);
        for (int i = 0; i < vector.Count; i++)
        {
            Span<byte> bytes = vector.ReadTLObject();
            if (bytes.Length < 4 ||
                new InputThemeSettings(bytes).Constructor !=
                Constructors.baseLayer_InputThemeSettings ||
                !TryReadSetting(new InputThemeSettings(bytes), out var setting))
            {
                Dispose(result);
                result.Clear();
                return false;
            }
            result.Add(setting);
        }
        return true;
    }

    protected static void Dispose(IEnumerable<ThemeSettingsInput> settings)
    {
        foreach (ThemeSettingsInput setting in settings) setting.Dispose();
    }

    protected static bool ValidBaseTheme(BaseThemeView view) =>
        view.Type is TLBaseTheme.BaseThemeType.BaseThemeClassic or
            TLBaseTheme.BaseThemeType.BaseThemeDay or
            TLBaseTheme.BaseThemeType.BaseThemeNight or
            TLBaseTheme.BaseThemeType.BaseThemeTinted or
            TLBaseTheme.BaseThemeType.BaseThemeArctic;

    protected static TLBytes AuthError() =>
        RpcErrorGenerator.GenerateError(401, "AUTH_KEY_INVALID"u8);
    protected static TLBytes Invalid() =>
        RpcErrorGenerator.GenerateError(400, "THEME_INVALID"u8);

    private static bool TryReadSetting(InputThemeSettings value,
        out ThemeSettingsInput result)
    {
        result = null!;
        if (!TryCloneBaseTheme(value.Get_BaseThemeView(),
                out TLBaseTheme baseTheme) ||
            !ThemeStore.ValidColor(value.AccentColor) ||
            value.Flags[3] && !ThemeStore.ValidColor(value.OutboxAccentColor))
            return false;
        int[] colors = value.Flags[0] ? value.MessageColors.ToArray() : [];
        if (colors.Length > 4 || colors.Any(color =>
                !ThemeStore.ValidColor(color)))
        {
            baseTheme.Dispose();
            return false;
        }
        WallpaperInput? wallpaper = null;
        TLWallPaperSettings? wallpaperSettings = null;
        if (value.Flags[1])
        {
            if (!TryReadWallpaper(value.Get_WallpaperView(), out var input) ||
                !value.Get_WallpaperSettingsView().Is(out WallPaperSettings ws) ||
                !WallpaperStore.IsSettingsValid(ws))
            {
                baseTheme.Dispose();
                return false;
            }
            wallpaper = input;
            wallpaperSettings = ws.Clone().Build();
        }
        result = new ThemeSettingsInput(baseTheme,
            value.MessageColorsAnimated, value.AccentColor,
            value.Flags[3] ? value.OutboxAccentColor : null, colors,
            wallpaper, wallpaperSettings);
        return true;
    }

    private static bool TryCloneBaseTheme(BaseThemeView view,
        out TLBaseTheme result)
    {
        if (view.Is(out BaseThemeClassic classic))
            result = classic.Clone().Build();
        else if (view.Is(out BaseThemeDay day)) result = day.Clone().Build();
        else if (view.Is(out BaseThemeNight night))
            result = night.Clone().Build();
        else if (view.Is(out BaseThemeTinted tinted))
            result = tinted.Clone().Build();
        else if (view.Is(out BaseThemeArctic arctic))
            result = arctic.Clone().Build();
        else
        {
            result = default;
            return false;
        }
        return true;
    }

    private static bool TryReadWallpaper(InputWallPaperView input,
        out WallpaperInput value)
    {
        if (input.Is(out InputWallPaper id))
        {
            value = new WallpaperInput(WallpaperInputKind.Id, id.Id,
                id.AccessHash, string.Empty);
            return id.Id > 0;
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
}
