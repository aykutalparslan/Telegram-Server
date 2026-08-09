// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

/// <summary>
/// Canonical key of the conversation a shared setting belongs to. A private pair
/// resolves to the same row from either side, so both participants read one theme
/// and one auto-delete timer.
/// </summary>
public readonly record struct ChatSettingsScope(int Type, long Id, long SecondaryId)
{
    public static ChatSettingsScope ForPrivatePair(long userA, long userB) =>
        new(ChatSettingsScopeType.PrivatePair, Math.Min(userA, userB),
            Math.Max(userA, userB));

    public static ChatSettingsScope ForChat(long chatId) =>
        new(ChatSettingsScopeType.Chat, chatId, 0);

    public static ChatSettingsScope ForChannel(long channelId) =>
        new(ChatSettingsScopeType.Channel, channelId, 0);
}

public readonly record struct ChatSettingsSnapshot(int TtlPeriod,
    string? ThemeEmoticon)
{
    public static ChatSettingsSnapshot Empty { get; } = new(0, null);

    public bool IsEmpty => TtlPeriod <= 0 && string.IsNullOrEmpty(ThemeEmoticon);
}

/// <summary>
/// Reads and writes the shared per-conversation settings, per-peer wallpapers,
/// account-wide defaults, and per-destination default senders.
/// </summary>
public sealed class ChatSettingsStore
{
    private readonly IMessagingSettingsRepository _messagingSettingsRepository;

    private const long SharedChannelWallpaperOwnerId = 0;

    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ChatSettingsStore(IUnitOfWork unitOfWork, IMessagingSettingsRepository messagingSettingsRepository, TimeProvider timeProvider)
    {
        _messagingSettingsRepository = messagingSettingsRepository;

        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async ValueTask<ChatSettingsSnapshot> GetAsync(ChatSettingsScope scope)
    {
        using TLChatMessagingSettings? stored = await _messagingSettingsRepository.GetChatSettingsAsync(scope.Type, scope.Id,
                scope.SecondaryId);
        if (stored == null)
        {
            return ChatSettingsSnapshot.Empty;
        }

        var settings = stored.Value.AsChatMessagingSettings();
        string? emoticon = settings.Flags[1]
            ? Encoding.UTF8.GetString(settings.ThemeEmoticon)
            : null;
        return new ChatSettingsSnapshot(settings.Flags[0] ? settings.TtlPeriod : 0,
            emoticon);
    }

    /// <summary>
    /// Replaces the row for a scope. A cleared value is written as a fresh row
    /// without its flag, because a generated builder cannot clear a flag that
    /// gates a value.
    /// </summary>
    public bool Put(ChatSettingsScope scope, ChatSettingsSnapshot snapshot)
    {
        if (snapshot.IsEmpty)
        {
            return _messagingSettingsRepository.DeleteChatSettings(
                scope.Type, scope.Id, scope.SecondaryId);
        }

        var builder = ChatMessagingSettings.Builder()
            .ScopeType(scope.Type)
            .ScopeId(scope.Id)
            .SecondaryId(scope.SecondaryId)
            .Date(UnixNow());
        if (snapshot.TtlPeriod > 0)
        {
            builder = builder.TtlPeriod(snapshot.TtlPeriod);
        }
        if (!string.IsNullOrEmpty(snapshot.ThemeEmoticon))
        {
            builder = builder.ThemeEmoticon(
                Encoding.UTF8.GetBytes(snapshot.ThemeEmoticon));
        }

        using TLChatMessagingSettings row = builder.Build();
        return _messagingSettingsRepository.PutChatSettings(row);
    }

    public async ValueTask<int> GetDefaultTtlPeriodAsync(long userId)
    {
        using TLAccountMessagingSettings? stored = await _messagingSettingsRepository.GetAccountSettingsAsync(userId);
        if (stored == null)
        {
            return 0;
        }
        var settings = stored.Value.AsAccountMessagingSettings();
        return settings.Flags[0] ? settings.DefaultTtlPeriod : 0;
    }

    public bool PutDefaultTtlPeriod(long userId, int period)
    {
        var builder = AccountMessagingSettings.Builder()
            .UserId(userId)
            .Date(UnixNow());
        if (period > 0)
        {
            builder = builder.DefaultTtlPeriod(period);
        }
        using TLAccountMessagingSettings row = builder.Build();
        return _messagingSettingsRepository.PutAccountSettings(row);
    }

    public async ValueTask<DialogPeerKey?> GetDefaultSendAsAsync(long userId,
        DialogPeerKey destination)
    {
        using TLDefaultSendAs? stored = await _messagingSettingsRepository
            .GetDefaultSendAsAsync(userId, (int)destination.Type, destination.Id);
        if (stored == null)
        {
            return null;
        }
        var sendAs = stored.Value.AsDefaultSendAs();
        return new DialogPeerKey((TLPeer.PeerType)sendAs.SendAsPeerType,
            sendAs.SendAsPeerId);
    }

    public bool PutDefaultSendAs(long userId, DialogPeerKey destination,
        DialogPeerKey sendAs)
    {
        using TLDefaultSendAs row = DefaultSendAs.Builder()
            .UserId(userId)
            .PeerType((int)destination.Type)
            .PeerId(destination.Id)
            .SendAsPeerType((int)sendAs.Type)
            .SendAsPeerId(sendAs.Id)
            .Date(UnixNow())
            .Build();
        return _messagingSettingsRepository.PutDefaultSendAs(row);
    }

    /// <summary>
    /// Gets the wallpaper a viewer has selected for a private peer. Private
    /// wallpaper state is deliberately viewer-specific rather than shared by the
    /// pair.
    /// </summary>
    public ValueTask<TLPeerWallpaper?> GetPrivateWallpaperAsync(long viewerId,
        long peerUserId) =>
        _messagingSettingsRepository.GetWallpaperAsync(viewerId,
            (int)TLPeer.PeerType.PeerUser, peerUserId);

    /// <summary>
    /// Replaces a viewer's private-peer wallpaper row. The wallpaper spans only
    /// need to remain valid for this synchronous call; the repository copies the
    /// generated owned row at its storage boundary.
    /// </summary>
    public bool PutPrivateWallpaper(long viewerId, long peerUserId, bool forBoth,
        bool overridden, ReadOnlySpan<byte> wallpaper = default,
        ReadOnlySpan<byte> previousWallpaper = default) =>
        PutWallpaper(viewerId, TLPeer.PeerType.PeerUser, peerUserId, forBoth,
            overridden, wallpaper, previousWallpaper);

    public bool DeletePrivateWallpaper(long viewerId, long peerUserId) =>
        _messagingSettingsRepository.DeleteWallpaper(viewerId,
            (int)TLPeer.PeerType.PeerUser, peerUserId);

    /// <summary>
    /// Gets the shared wallpaper for a channel. Owner id zero is the canonical
    /// storage key so the state is visible to every current and future member.
    /// </summary>
    public ValueTask<TLPeerWallpaper?> GetChannelWallpaperAsync(long channelId) =>
        _messagingSettingsRepository.GetWallpaperAsync(
            SharedChannelWallpaperOwnerId, (int)TLPeer.PeerType.PeerChannel,
            channelId);

    public bool PutChannelWallpaper(long channelId, ReadOnlySpan<byte> wallpaper) =>
        PutWallpaper(SharedChannelWallpaperOwnerId, TLPeer.PeerType.PeerChannel,
            channelId, false, false, wallpaper, default);

    public bool DeleteChannelWallpaper(long channelId) =>
        _messagingSettingsRepository.DeleteWallpaper(
            SharedChannelWallpaperOwnerId, (int)TLPeer.PeerType.PeerChannel,
            channelId);

    private bool PutWallpaper(long ownerUserId, TLPeer.PeerType peerType,
        long peerId, bool forBoth, bool overridden, ReadOnlySpan<byte> wallpaper,
        ReadOnlySpan<byte> previousWallpaper)
    {
        var builder = PeerWallpaper.Builder()
            .ForBoth(forBoth)
            .Overridden(overridden)
            .UserId(ownerUserId)
            .PeerType((int)peerType)
            .PeerId(peerId)
            .Date(UnixNow());
        if (!wallpaper.IsEmpty)
        {
            builder = builder.Wallpaper(wallpaper);
        }
        if (!previousWallpaper.IsEmpty)
        {
            builder = builder.PreviousWallpaper(previousWallpaper);
        }

        using TLPeerWallpaper row = builder.Build();
        return _messagingSettingsRepository.PutWallpaper(row);
    }

    private int UnixNow() =>
        checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());
}
