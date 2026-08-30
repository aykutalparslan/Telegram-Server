// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;
using Ferrite.Data.Models;

namespace Ferrite.Data.Repositories;

public sealed class MessagingSettingsRepository : IMessagingSettingsRepository
{
    private readonly IKVStore _chatSettings;
    private readonly IKVStore _wallpapers;
    private readonly IKVStore _accountSettings;
    private readonly IKVStore _defaultSendAs;

    public MessagingSettingsRepository(IKVStore chatSettings, IKVStore wallpapers,
        IKVStore accountSettings, IKVStore defaultSendAs)
    {
        _chatSettings = chatSettings;
        _wallpapers = wallpapers;
        _accountSettings = accountSettings;
        _defaultSendAs = defaultSendAs;
        chatSettings.SetSchema(new TableDefinition("ferrite", "chat_messaging_settings",
            new KeyDefinition("pk",
                new DataColumn { Name = "scope_type", Type = DataType.Int },
                new DataColumn { Name = "scope_id", Type = DataType.Long },
                new DataColumn { Name = "secondary_id", Type = DataType.Long })));
        wallpapers.SetSchema(new TableDefinition("ferrite", "peer_wallpapers",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "peer_type", Type = DataType.Int },
                new DataColumn { Name = "peer_id", Type = DataType.Long })));
        accountSettings.SetSchema(new TableDefinition("ferrite", "account_messaging_settings",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long })));
        defaultSendAs.SetSchema(new TableDefinition("ferrite", "default_send_as",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "peer_type", Type = DataType.Int },
                new DataColumn { Name = "peer_id", Type = DataType.Long })));
    }

    public bool PutChatSettings(TLChatMessagingSettings settings)
    {
        var info = settings.AsChatMessagingSettings();
        return _chatSettings.Put(settings.AsSpan().ToArray(), info.ScopeType,
            info.ScopeId, info.SecondaryId);
    }

    public async ValueTask<TLChatMessagingSettings?> GetChatSettingsAsync(int scopeType,
        long scopeId, long secondaryId)
    {
        byte[]? bytes = await _chatSettings.GetAsync(scopeType, scopeId, secondaryId);
        return bytes is { Length: > 0 }
            ? new TLChatMessagingSettings(bytes, 0, bytes.Length)
            : null;
    }

    public bool DeleteChatSettings(int scopeType, long scopeId, long secondaryId) =>
        _chatSettings.Delete(scopeType, scopeId, secondaryId);

    public bool PutWallpaper(TLPeerWallpaper wallpaper)
    {
        var info = wallpaper.AsPeerWallpaper();
        return _wallpapers.Put(wallpaper.AsSpan().ToArray(), info.UserId,
            info.PeerType, info.PeerId);
    }

    public async ValueTask<TLPeerWallpaper?> GetWallpaperAsync(long userId,
        int peerType, long peerId)
    {
        byte[]? bytes = await _wallpapers.GetAsync(userId, peerType, peerId);
        return bytes is { Length: > 0 }
            ? new TLPeerWallpaper(bytes, 0, bytes.Length)
            : null;
    }

    public bool DeleteWallpaper(long userId, int peerType, long peerId) =>
        _wallpapers.Delete(userId, peerType, peerId);

    public bool PutAccountSettings(TLAccountMessagingSettings settings)
    {
        var info = settings.AsAccountMessagingSettings();
        return _accountSettings.Put(settings.AsSpan().ToArray(), info.UserId);
    }

    public async ValueTask<TLAccountMessagingSettings?> GetAccountSettingsAsync(
        long userId)
    {
        byte[]? bytes = await _accountSettings.GetAsync(userId);
        return bytes is { Length: > 0 }
            ? new TLAccountMessagingSettings(bytes, 0, bytes.Length)
            : null;
    }

    public bool PutDefaultSendAs(TLDefaultSendAs sendAs)
    {
        var info = sendAs.AsDefaultSendAs();
        return _defaultSendAs.Put(sendAs.AsSpan().ToArray(), info.UserId,
            info.PeerType, info.PeerId);
    }

    public async ValueTask<TLDefaultSendAs?> GetDefaultSendAsAsync(long userId,
        int peerType, long peerId)
    {
        byte[]? bytes = await _defaultSendAs.GetAsync(userId, peerType, peerId);
        return bytes is { Length: > 0 }
            ? new TLDefaultSendAs(bytes, 0, bytes.Length)
            : null;
    }

    public bool DeleteDefaultSendAs(long userId, int peerType, long peerId) =>
        _defaultSendAs.Delete(userId, peerType, peerId);
}
