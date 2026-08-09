// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IAccountSettingsRepository
{
    bool PutSettings(TLAccountSettingsState state);
    ValueTask<TLAccountSettingsState?> GetSettingsAsync(long userId);

    bool PutAutoDownloadSettings(TLAutoDownloadSettingsState state);
    ValueTask<TLAutoDownloadSettingsState?> GetAutoDownloadSettingsAsync(
        long userId);

    bool PutAutoSaveSettings(TLAutoSaveSettingsState state);
    ValueTask<TLAutoSaveSettingsState?> GetAutoSaveSettingsAsync(long userId,
        int scope, int peerType, long peerId);
    ValueTask<IReadOnlyCollection<TLAutoSaveSettingsState>>
        GetAutoSaveSettingsAsync(long userId);
    bool DeleteAutoSaveSettings(long userId, int scope, int peerType,
        long peerId);

    bool PutTakeoutSession(TLTakeoutSessionState state);
    ValueTask<TLTakeoutSessionState?> GetTakeoutSessionAsync(long id);
    ValueTask<TLTakeoutSessionState?> GetTakeoutSessionByAuthKeyAsync(
        long authKeyId);
    bool DeleteTakeoutSession(long id);

    bool PutProfile(TLAccountProfileState state);
    ValueTask<TLAccountProfileState?> GetProfileAsync(long userId);

    bool PutCloseFriend(TLCloseFriendState state);
    ValueTask<IReadOnlyCollection<TLCloseFriendState>> GetCloseFriendsAsync(
        long userId);
    ValueTask<bool> IsCloseFriendAsync(long userId, long closeFriendId);
    bool DeleteCloseFriends(long userId);

    bool PutContactToken(TLContactTokenState state);
    ValueTask<TLContactTokenState?> GetContactTokenAsync(string token);
    ValueTask<IReadOnlyCollection<TLContactTokenState>> GetContactTokensAsync(
        long userId);
    bool DeleteContactToken(long userId, string token);

    bool PutWallpaperCatalog(TLWallpaperCatalogState state);
    ValueTask<TLWallpaperCatalogState?> GetWallpaperCatalogAsync(long id);
    ValueTask<TLWallpaperCatalogState?> GetWallpaperCatalogBySlugAsync(
        string slug);
    ValueTask<IReadOnlyCollection<TLWallpaperCatalogState>>
        GetWallpaperCatalogAsync();

    bool PutAccountWallpaper(TLAccountWallpaperState state);
    ValueTask<TLAccountWallpaperState?> GetAccountWallpaperAsync(long userId,
        long wallpaperId);
    ValueTask<IReadOnlyCollection<TLAccountWallpaperState>>
        GetAccountWallpapersAsync(long userId);
    bool DeleteAccountWallpaper(long userId, long wallpaperId);
    bool DeleteAccountWallpapers(long userId);

    bool PutThemeCatalog(TLThemeCatalogState state);
    ValueTask<TLThemeCatalogState?> GetThemeCatalogAsync(long id);
    ValueTask<TLThemeCatalogState?> GetThemeCatalogBySlugAsync(string slug);
    ValueTask<IReadOnlyCollection<TLThemeCatalogState>> GetThemeCatalogAsync();
    bool DeleteThemeCatalog(long id, string slug);

    bool PutAccountTheme(TLAccountThemeState state);
    ValueTask<TLAccountThemeState?> GetAccountThemeAsync(long userId,
        long themeId);
    ValueTask<IReadOnlyCollection<TLAccountThemeState>> GetAccountThemesAsync(
        long userId);
    bool DeleteAccountTheme(long userId, long themeId);
    bool DeleteAccountThemes(long userId);

    bool PutRingtones(TLAccountRingtonesState state);
    ValueTask<TLAccountRingtonesState?> GetRingtonesAsync(long userId);

    bool PutProfileMusic(TLProfileMusicState state);
    ValueTask<TLProfileMusicState?> GetProfileMusicAsync(long userId);
}
