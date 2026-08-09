// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public static class ChatSettingsScopeType
{
    public const int PrivatePair = 0;
    public const int Chat = 1;
    public const int Channel = 2;
}

public interface IMessagingSettingsRepository
{
    bool PutChatSettings(TLChatMessagingSettings settings);
    ValueTask<TLChatMessagingSettings?> GetChatSettingsAsync(int scopeType,
        long scopeId, long secondaryId);
    bool DeleteChatSettings(int scopeType, long scopeId, long secondaryId);

    bool PutWallpaper(TLPeerWallpaper wallpaper);
    ValueTask<TLPeerWallpaper?> GetWallpaperAsync(long userId, int peerType,
        long peerId);
    bool DeleteWallpaper(long userId, int peerType, long peerId);

    bool PutAccountSettings(TLAccountMessagingSettings settings);
    ValueTask<TLAccountMessagingSettings?> GetAccountSettingsAsync(long userId);

    bool PutDefaultSendAs(TLDefaultSendAs sendAs);
    ValueTask<TLDefaultSendAs?> GetDefaultSendAsAsync(long userId, int peerType,
        long peerId);
    bool DeleteDefaultSendAs(long userId, int peerType, long peerId);
}
