// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class AccountSettingsRepository : IAccountSettingsRepository
{
    private readonly IKVStore _settings;
    private readonly IKVStore _autoDownload;
    private readonly IKVStore _autoSave;
    private readonly IKVStore _takeout;
    private readonly IKVStore _profiles;
    private readonly IKVStore _closeFriends;
    private readonly IKVStore _contactTokens;
    private readonly IKVStore _wallpaperCatalog;
    private readonly IKVStore _accountWallpapers;
    private readonly IKVStore _themeCatalog;
    private readonly IKVStore _accountThemes;
    private readonly IKVStore _ringtones;
    private readonly IKVStore _profileMusic;

    public AccountSettingsRepository(IKVStore settings, IKVStore autoDownload,
        IKVStore autoSave, IKVStore takeout, IKVStore profiles,
        IKVStore closeFriends, IKVStore contactTokens,
        IKVStore wallpaperCatalog, IKVStore accountWallpapers,
        IKVStore themeCatalog, IKVStore accountThemes, IKVStore ringtones,
        IKVStore profileMusic)
    {
        _settings = settings;
        _autoDownload = autoDownload;
        _autoSave = autoSave;
        _takeout = takeout;
        _profiles = profiles;
        _closeFriends = closeFriends;
        _contactTokens = contactTokens;
        _wallpaperCatalog = wallpaperCatalog;
        _accountWallpapers = accountWallpapers;
        _themeCatalog = themeCatalog;
        _accountThemes = accountThemes;
        _ringtones = ringtones;
        _profileMusic = profileMusic;

        settings.SetSchema(new TableDefinition("ferrite", "account_settings",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long })));
        autoDownload.SetSchema(new TableDefinition("ferrite",
            "account_auto_download_settings", new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long })));
        autoSave.SetSchema(new TableDefinition("ferrite",
            "account_auto_save_settings", new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "scope", Type = DataType.Int },
                new DataColumn { Name = "peer_type", Type = DataType.Int },
                new DataColumn { Name = "peer_id", Type = DataType.Long })));
        takeout.SetSchema(new TableDefinition("ferrite", "takeout_sessions",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long },
                new DataColumn { Name = "id", Type = DataType.Long }),
            new KeyDefinition("by_id",
                new DataColumn { Name = "id", Type = DataType.Long })));
        profiles.SetSchema(new TableDefinition("ferrite", "account_profiles",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long })));
        closeFriends.SetSchema(new TableDefinition("ferrite", "close_friends",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "close_friend_id", Type = DataType.Long })));
        // "token" is a reserved word in CQL, so a column of that name cannot be
        // created on the Cassandra backend. The durable column is named
        // contact_token; the secondary index keeps its by_token name because that
        // becomes a table-name suffix, not an identifier.
        contactTokens.SetSchema(new TableDefinition("ferrite", "contact_tokens",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "contact_token", Type = DataType.String }),
            new KeyDefinition("by_token",
                new DataColumn { Name = "contact_token", Type = DataType.String })));
        wallpaperCatalog.SetSchema(new TableDefinition("ferrite",
            "wallpaper_catalog", new KeyDefinition("pk",
                new DataColumn { Name = "wallpaper_id", Type = DataType.Long },
                new DataColumn { Name = "slug", Type = DataType.String }),
            new KeyDefinition("by_id",
                new DataColumn { Name = "wallpaper_id", Type = DataType.Long }),
            new KeyDefinition("by_slug",
                new DataColumn { Name = "slug", Type = DataType.String })));
        accountWallpapers.SetSchema(new TableDefinition("ferrite",
            "account_wallpapers", new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "wallpaper_id", Type = DataType.Long })));
        themeCatalog.SetSchema(new TableDefinition("ferrite", "theme_catalog",
            new KeyDefinition("pk",
                new DataColumn { Name = "theme_id", Type = DataType.Long },
                new DataColumn { Name = "slug", Type = DataType.String }),
            new KeyDefinition("by_id",
                new DataColumn { Name = "theme_id", Type = DataType.Long }),
            new KeyDefinition("by_slug",
                new DataColumn { Name = "slug", Type = DataType.String })));
        accountThemes.SetSchema(new TableDefinition("ferrite", "account_themes",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "theme_id", Type = DataType.Long })));
        ringtones.SetSchema(new TableDefinition("ferrite", "account_ringtones",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long })));
        profileMusic.SetSchema(new TableDefinition("ferrite", "profile_music",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long })));
    }

    public bool PutSettings(TLAccountSettingsState state)
    {
        var row = state.AsAccountSettingsState();
        return _settings.Put(state.AsSpan().ToArray(), row.UserId);
    }

    public async ValueTask<TLAccountSettingsState?> GetSettingsAsync(long userId) =>
        WrapSettings(await _settings.GetAsync(userId));

    public bool PutAutoDownloadSettings(TLAutoDownloadSettingsState state)
    {
        var row = state.AsAutoDownloadSettingsState();
        return _autoDownload.Put(state.AsSpan().ToArray(), row.UserId);
    }

    public async ValueTask<TLAutoDownloadSettingsState?>
        GetAutoDownloadSettingsAsync(long userId) =>
        WrapAutoDownload(await _autoDownload.GetAsync(userId));

    public bool PutAutoSaveSettings(TLAutoSaveSettingsState state)
    {
        var row = state.AsAutoSaveSettingsState();
        return _autoSave.Put(state.AsSpan().ToArray(), row.UserId, row.Scope,
            row.PeerType, row.PeerId);
    }

    public async ValueTask<TLAutoSaveSettingsState?> GetAutoSaveSettingsAsync(
        long userId, int scope, int peerType, long peerId) =>
        WrapAutoSave(await _autoSave.GetAsync(userId, scope, peerType, peerId));

    public async ValueTask<IReadOnlyCollection<TLAutoSaveSettingsState>>
        GetAutoSaveSettingsAsync(long userId)
    {
        var rows = new List<TLAutoSaveSettingsState>();
        await foreach (byte[] bytes in _autoSave.IterateAsync(userId))
        {
            rows.Add(new TLAutoSaveSettingsState(bytes, 0, bytes.Length));
        }
        return rows;
    }

    public bool DeleteAutoSaveSettings(long userId, int scope, int peerType,
        long peerId) => _autoSave.Delete(userId, scope, peerType, peerId);

    public bool PutTakeoutSession(TLTakeoutSessionState state)
    {
        var row = state.AsTakeoutSessionState();
        return _takeout.Put(state.AsSpan().ToArray(), row.AuthKeyId, row.Id);
    }

    public async ValueTask<TLTakeoutSessionState?> GetTakeoutSessionAsync(long id) =>
        WrapTakeout(await _takeout.GetBySecondaryIndexAsync("by_id", id));

    public async ValueTask<TLTakeoutSessionState?>
        GetTakeoutSessionByAuthKeyAsync(long authKeyId)
    {
        await foreach (byte[] bytes in _takeout.IterateAsync(authKeyId))
        {
            return new TLTakeoutSessionState(bytes, 0, bytes.Length);
        }
        return null;
    }

    public bool DeleteTakeoutSession(long id)
    {
        byte[]? bytes = _takeout.GetBySecondaryIndex("by_id", id);
        if (bytes is not { Length: > 0 }) return false;
        var row = new TLTakeoutSessionState(bytes, 0, bytes.Length)
            .AsTakeoutSessionState();
        return _takeout.Delete(row.AuthKeyId, row.Id);
    }

    public bool PutProfile(TLAccountProfileState state)
    {
        var row = state.AsAccountProfileState();
        return _profiles.Put(state.AsSpan().ToArray(), row.UserId);
    }

    public async ValueTask<TLAccountProfileState?> GetProfileAsync(long userId)
    {
        byte[]? bytes = await _profiles.GetAsync(userId);
        return bytes is { Length: > 0 }
            ? new TLAccountProfileState(bytes, 0, bytes.Length) : null;
    }

    public bool PutCloseFriend(TLCloseFriendState state)
    {
        var row = state.AsCloseFriendState();
        return _closeFriends.Put(state.AsSpan().ToArray(), row.UserId,
            row.CloseFriendId);
    }

    public async ValueTask<IReadOnlyCollection<TLCloseFriendState>>
        GetCloseFriendsAsync(long userId)
    {
        var rows = new List<TLCloseFriendState>();
        await foreach (byte[] bytes in _closeFriends.IterateAsync(userId))
        {
            rows.Add(new TLCloseFriendState(bytes, 0, bytes.Length));
        }
        return rows;
    }

    public async ValueTask<bool> IsCloseFriendAsync(long userId,
        long closeFriendId) =>
        await _closeFriends.GetAsync(userId, closeFriendId) is { Length: > 0 };

    public bool DeleteCloseFriends(long userId) => _closeFriends.Delete(userId);

    public bool PutContactToken(TLContactTokenState state)
    {
        var row = state.AsContactTokenState();
        return _contactTokens.Put(state.AsSpan().ToArray(), row.UserId,
            System.Text.Encoding.UTF8.GetString(row.Token));
    }

    public async ValueTask<TLContactTokenState?> GetContactTokenAsync(string token)
    {
        byte[]? bytes = await _contactTokens.GetBySecondaryIndexAsync("by_token",
            token);
        return bytes is { Length: > 0 }
            ? new TLContactTokenState(bytes, 0, bytes.Length) : null;
    }

    public async ValueTask<IReadOnlyCollection<TLContactTokenState>>
        GetContactTokensAsync(long userId)
    {
        var rows = new List<TLContactTokenState>();
        await foreach (byte[] bytes in _contactTokens.IterateAsync(userId))
        {
            rows.Add(new TLContactTokenState(bytes, 0, bytes.Length));
        }
        return rows;
    }

    public bool DeleteContactToken(long userId, string token) =>
        _contactTokens.Delete(userId, token);

    public bool PutWallpaperCatalog(TLWallpaperCatalogState state)
    {
        var row = state.AsWallpaperCatalogState();
        WallPaperView wallpaper = row.Get_WallpaperView();
        long id = wallpaper.Is(out WallPaper value) ? value.Id
            : wallpaper.AsWallPaperNoFile().Id;
        string slug = wallpaper.Is(out WallPaper file)
            ? System.Text.Encoding.UTF8.GetString(file.Slug) : string.Empty;
        return _wallpaperCatalog.Put(state.AsSpan().ToArray(), id, slug);
    }

    public async ValueTask<TLWallpaperCatalogState?> GetWallpaperCatalogAsync(
        long id) => WrapWallpaperCatalog(await _wallpaperCatalog
            .GetBySecondaryIndexAsync("by_id", id));

    public async ValueTask<TLWallpaperCatalogState?>
        GetWallpaperCatalogBySlugAsync(string slug) => WrapWallpaperCatalog(
            await _wallpaperCatalog.GetBySecondaryIndexAsync("by_slug", slug));

    public async ValueTask<IReadOnlyCollection<TLWallpaperCatalogState>>
        GetWallpaperCatalogAsync()
    {
        var rows = new List<TLWallpaperCatalogState>();
        await foreach (byte[] bytes in _wallpaperCatalog.IterateAsync())
            rows.Add(new TLWallpaperCatalogState(bytes, 0, bytes.Length));
        return rows;
    }

    public bool PutAccountWallpaper(TLAccountWallpaperState state)
    {
        var row = state.AsAccountWallpaperState();
        return _accountWallpapers.Put(state.AsSpan().ToArray(), row.UserId,
            row.WallpaperId);
    }

    public async ValueTask<TLAccountWallpaperState?> GetAccountWallpaperAsync(
        long userId, long wallpaperId) => WrapAccountWallpaper(
            await _accountWallpapers.GetAsync(userId, wallpaperId));

    public async ValueTask<IReadOnlyCollection<TLAccountWallpaperState>>
        GetAccountWallpapersAsync(long userId)
    {
        var rows = new List<TLAccountWallpaperState>();
        await foreach (byte[] bytes in _accountWallpapers.IterateAsync(userId))
            rows.Add(new TLAccountWallpaperState(bytes, 0, bytes.Length));
        return rows;
    }

    public bool DeleteAccountWallpaper(long userId, long wallpaperId) =>
        _accountWallpapers.Delete(userId, wallpaperId);

    public bool DeleteAccountWallpapers(long userId) =>
        _accountWallpapers.Delete(userId);

    public bool PutThemeCatalog(TLThemeCatalogState state)
    {
        var row = state.AsThemeCatalogState();
        Theme theme = row.Get_ThemeView().AsTheme();
        return _themeCatalog.Put(state.AsSpan().ToArray(), theme.Id,
            Normalize(theme.Slug));
    }

    public async ValueTask<TLThemeCatalogState?> GetThemeCatalogAsync(long id) =>
        WrapThemeCatalog(await _themeCatalog.GetBySecondaryIndexAsync("by_id", id));

    public async ValueTask<TLThemeCatalogState?> GetThemeCatalogBySlugAsync(
        string slug) => WrapThemeCatalog(await _themeCatalog
            .GetBySecondaryIndexAsync("by_slug", Normalize(slug)));

    public async ValueTask<IReadOnlyCollection<TLThemeCatalogState>>
        GetThemeCatalogAsync()
    {
        var rows = new List<TLThemeCatalogState>();
        await foreach (byte[] bytes in _themeCatalog.IterateAsync())
            rows.Add(new TLThemeCatalogState(bytes, 0, bytes.Length));
        return rows;
    }

    public bool DeleteThemeCatalog(long id, string slug) =>
        _themeCatalog.Delete(id, Normalize(slug));

    public bool PutAccountTheme(TLAccountThemeState state)
    {
        var row = state.AsAccountThemeState();
        return _accountThemes.Put(state.AsSpan().ToArray(), row.UserId,
            row.ThemeId);
    }

    public async ValueTask<TLAccountThemeState?> GetAccountThemeAsync(long userId,
        long themeId) => WrapAccountTheme(await _accountThemes.GetAsync(userId,
        themeId));

    public async ValueTask<IReadOnlyCollection<TLAccountThemeState>>
        GetAccountThemesAsync(long userId)
    {
        var rows = new List<TLAccountThemeState>();
        await foreach (byte[] bytes in _accountThemes.IterateAsync(userId))
            rows.Add(new TLAccountThemeState(bytes, 0, bytes.Length));
        return rows;
    }

    public bool DeleteAccountTheme(long userId, long themeId) =>
        _accountThemes.Delete(userId, themeId);

    public bool DeleteAccountThemes(long userId) => _accountThemes.Delete(userId);

    public bool PutRingtones(TLAccountRingtonesState state)
    {
        var row = state.AsAccountRingtonesState();
        return _ringtones.Put(state.AsSpan().ToArray(), row.UserId);
    }

    public async ValueTask<TLAccountRingtonesState?> GetRingtonesAsync(
        long userId) => WrapRingtones(await _ringtones.GetAsync(userId));

    public bool PutProfileMusic(TLProfileMusicState state)
    {
        var row = state.AsProfileMusicState();
        return _profileMusic.Put(state.AsSpan().ToArray(), row.UserId);
    }

    public async ValueTask<TLProfileMusicState?> GetProfileMusicAsync(
        long userId) => WrapProfileMusic(await _profileMusic.GetAsync(userId));

    private static TLAccountSettingsState? WrapSettings(byte[]? bytes) =>
        bytes is { Length: > 0 }
            ? new TLAccountSettingsState(bytes, 0, bytes.Length) : null;

    private static TLAutoDownloadSettingsState? WrapAutoDownload(byte[]? bytes) =>
        bytes is { Length: > 0 }
            ? new TLAutoDownloadSettingsState(bytes, 0, bytes.Length) : null;

    private static TLAutoSaveSettingsState? WrapAutoSave(byte[]? bytes) =>
        bytes is { Length: > 0 }
            ? new TLAutoSaveSettingsState(bytes, 0, bytes.Length) : null;

    private static TLTakeoutSessionState? WrapTakeout(byte[]? bytes) =>
        bytes is { Length: > 0 }
            ? new TLTakeoutSessionState(bytes, 0, bytes.Length) : null;

    private static TLWallpaperCatalogState? WrapWallpaperCatalog(byte[]? bytes) =>
        bytes is { Length: > 0 }
            ? new TLWallpaperCatalogState(bytes, 0, bytes.Length) : null;

    private static TLAccountWallpaperState? WrapAccountWallpaper(byte[]? bytes) =>
        bytes is { Length: > 0 }
            ? new TLAccountWallpaperState(bytes, 0, bytes.Length) : null;

    private static TLThemeCatalogState? WrapThemeCatalog(byte[]? bytes) =>
        bytes is { Length: > 0 }
            ? new TLThemeCatalogState(bytes, 0, bytes.Length) : null;

    private static TLAccountThemeState? WrapAccountTheme(byte[]? bytes) =>
        bytes is { Length: > 0 }
            ? new TLAccountThemeState(bytes, 0, bytes.Length) : null;

    private static TLAccountRingtonesState? WrapRingtones(byte[]? bytes) =>
        bytes is { Length: > 0 }
            ? new TLAccountRingtonesState(bytes, 0, bytes.Length) : null;

    private static TLProfileMusicState? WrapProfileMusic(byte[]? bytes) =>
        bytes is { Length: > 0 }
            ? new TLProfileMusicState(bytes, 0, bytes.Length) : null;

    private static string Normalize(ReadOnlySpan<byte> value) =>
        System.Text.Encoding.UTF8.GetString(value).Trim().ToLowerInvariant();

    private static string Normalize(string value) =>
        value.Trim().ToLowerInvariant();
}
