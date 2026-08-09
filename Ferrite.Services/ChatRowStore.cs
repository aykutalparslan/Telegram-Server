// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

// Compact chat/channel row mutation + stored-photo lookup, backed by the chat and
// photo repositories. Consolidates the row-mutation helpers formerly duplicated
// across MessagesService and ChannelsService (chats and channels both persist as
// the compact TLChat row in ChatRepository).
public sealed class ChatRowStore
{
    private readonly IChatRepository _chatRepository;
    private readonly IPhotoRepository _photoRepository;

    private readonly IUnitOfWork _unitOfWork;

    public ChatRowStore(IUnitOfWork unitOfWork, IChatRepository chatRepository, IPhotoRepository photoRepository)
    {
        _chatRepository = chatRepository;
        _photoRepository = photoRepository;

        _unitOfWork = unitOfWork;
    }

    public byte[] UpdateStoredChatTitle(byte[] chatBytes, byte[] title)
    {
        using var storedChat = new TLChat(chatBytes, 0, chatBytes.Length);
        var chat = storedChat.AsChat();
        using TLChat updatedChat = chat.Clone()
            .Title(title)
            .Version(chat.Version + 1)
            .Build();
        _chatRepository.PutChat(updatedChat);
        return updatedChat.AsSpan().ToArray();
    }

    public byte[] UpdateStoredChatPhotoEmpty(byte[] chatBytes)
    {
        using var storedChat = new TLChat(chatBytes, 0, chatBytes.Length);
        var chat = storedChat.AsChat();
        using var emptyPhoto = ChatPhotoEmpty.Builder().Build();
        using TLChat updatedChat = chat.Clone()
            .Photo(emptyPhoto.ToReadOnlySpan())
            .Version(chat.Version + 1)
            .Build();
        _chatRepository.PutChat(updatedChat);
        return updatedChat.AsSpan().ToArray();
    }

    public byte[] UpdateStoredChatPhoto(byte[] chatBytes, long photoId)
    {
        using var storedChat = new TLChat(chatBytes, 0, chatBytes.Length);
        var chat = storedChat.AsChat();
        byte[] chatPhoto = ChatPhotos.BuildCompactChatPhoto(photoId);
        using TLChat updatedChat = chat.Clone()
            .Photo(chatPhoto)
            .Version(chat.Version + 1)
            .Build();
        _chatRepository.PutChat(updatedChat);
        return updatedChat.AsSpan().ToArray();
    }

    public byte[] UpdateStoredChatMembership(byte[] chatBytes, int participantsCountDelta)
    {
        using var storedChat = new TLChat(chatBytes, 0, chatBytes.Length);
        var chat = storedChat.AsChat();
        using TLChat updatedChat = chat.Clone()
            .ParticipantsCount(chat.ParticipantsCount + participantsCountDelta)
            .Version(chat.Version + 1)
            .Build();
        _chatRepository.PutChat(updatedChat);
        return updatedChat.AsSpan().ToArray();
    }

    // call_active/call_not_empty on the compact basic-group row. The generated flag
    // builder assigns the flag bit rather than only setting it, so passing false
    // genuinely clears a previously linked call.
    public byte[] UpdateStoredChatCallState(byte[] chatBytes, bool callActive,
        bool callNotEmpty)
    {
        using var storedChat = new TLChat(chatBytes, 0, chatBytes.Length);
        var chat = storedChat.AsChat();
        using TLChat updatedChat = chat.Clone()
            .CallActive(callActive)
            .CallNotEmpty(callNotEmpty)
            .Version(chat.Version + 1)
            .Build();
        _chatRepository.PutChat(updatedChat);
        return updatedChat.AsSpan().ToArray();
    }

    public byte[] UpdateStoredChatDefaultBannedRights(byte[] chatBytes, byte[] rightsBytes)
    {
        using var storedChat = new TLChat(chatBytes, 0, chatBytes.Length);
        var chat = storedChat.AsChat();
        using TLChat updatedChat = chat.Clone()
            .DefaultBannedRights(rightsBytes)
            .Version(chat.Version + 1)
            .Build();
        _chatRepository.PutChat(updatedChat);
        return updatedChat.AsSpan().ToArray();
    }

    public byte[] UpdateStoredChannelTitle(byte[] channelBytes, byte[] title)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        var channel = stored.AsChannel();
        using TLChat updated = channel.Clone().Title(title).Build();
        _chatRepository.PutChat(updated);
        return updated.AsSpan().ToArray();
    }

    public byte[] UpdateStoredChannelPhotoEmpty(byte[] channelBytes)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        var channel = stored.AsChannel();
        using var emptyPhoto = ChatPhotoEmpty.Builder().Build();
        using TLChat updated = channel.Clone().Photo(emptyPhoto.ToReadOnlySpan()).Build();
        _chatRepository.PutChat(updated);
        return updated.AsSpan().ToArray();
    }

    public byte[] UpdateStoredChannelPhoto(byte[] channelBytes, long photoId)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        var channel = stored.AsChannel();
        byte[] chatPhoto = ChatPhotos.BuildCompactChatPhoto(photoId);
        using TLChat updated = channel.Clone().Photo(chatPhoto).Build();
        _chatRepository.PutChat(updated);
        return updated.AsSpan().ToArray();
    }

    public byte[] UpdateStoredChannelMembership(byte[] channelBytes, int delta)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        var channel = stored.AsChannel();
        int newCount = Math.Max(0, channel.ParticipantsCount + delta);
        using TLChat updated = channel.Clone().ParticipantsCount(newCount).Build();
        _chatRepository.PutChat(updated);
        return updated.AsSpan().ToArray();
    }

    public byte[] UpdateStoredChannelParticipantsCount(byte[] channelBytes, int delta)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        var channel = stored.AsChannel();
        int newCount = Math.Max(0, channel.ParticipantsCount + delta);
        using TLChat updated = channel.Clone().ParticipantsCount(newCount).Build();
        _chatRepository.PutChat(updated);
        return updated.AsSpan().ToArray();
    }

    // The channel counterpart. Channel rows carry no version field, so linkage
    // changes never bump one (matching every other channel mutation here).
    public byte[] UpdateStoredChannelCallState(byte[] channelBytes, bool callActive,
        bool callNotEmpty)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        var channel = stored.AsChannel();
        using TLChat updated = channel.Clone()
            .CallActive(callActive)
            .CallNotEmpty(callNotEmpty)
            .Build();
        _chatRepository.PutChat(updated);
        return updated.AsSpan().ToArray();
    }

    // noforwards is a bare flags.N?true flag, so the builder clears it on false.
    public byte[] UpdateStoredChatNoforwards(byte[] chatBytes, bool enabled)
    {
        using var stored = new TLChat(chatBytes, 0, chatBytes.Length);
        var chat = stored.AsChat();
        using TLChat updated = chat.Clone()
            .Noforwards(enabled)
            .Version(chat.Version + 1)
            .Build();
        _chatRepository.PutChat(updated);
        return updated.AsSpan().ToArray();
    }

    public byte[] UpdateStoredChannelNoforwards(byte[] channelBytes, bool enabled)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        var channel = stored.AsChannel();
        using TLChat updated = channel.Clone()
            .Noforwards(enabled)
            .Build();
        _chatRepository.PutChat(updated);
        return updated.AsSpan().ToArray();
    }

    public byte[] UpdateStoredChannelDefaultBannedRights(byte[] channelBytes, byte[] rightsBytes)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        var channel = stored.AsChannel();
        using TLChat updated = channel.Clone()
            .DefaultBannedRights(rightsBytes)
            .Build();
        _chatRepository.PutChat(updated);
        return updated.AsSpan().ToArray();
    }

    public byte[] UpdateStoredChannelForumState(byte[] channelBytes, bool forum,
        bool forumTabs)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        var channel = stored.AsChannel();
        // Only a megagroup can be a forum, so `megagroup` is asserted rather than
        // copied; everything else survives through the full-field rebuild core.
        Flags flags = channel.Flags;
        flags[8] = true;
        flags[30] = forum;
        Flags flags2 = channel.Flags2;
        flags2[19] = forumTabs;
        using TLChat updated = ChannelRows.WithFlags(channel, flags, flags2);
        _chatRepository.PutChat(updated);
        return updated.AsSpan().ToArray();
    }

    public async Task UpdateStoredChannelForumTabsAsync(long channelId, bool tabs)
    {
        using var stored = await _chatRepository.GetFullInfoAsync(channelId);
        byte[] about = Array.Empty<byte>();
        int pinned = 0;
        byte[]? defaultRights = null;
        byte[]? reactions = null;
        int? reactionsLimit = null;
        long migratedFrom = 0;
        int migratedMax = 0;
        if (stored != null)
        {
            var info = stored.Value.AsChatFullInfo();
            about = info.About.ToArray();
            pinned = info.PinnedMsgId;
            if (info.Flags[1]) defaultRights = info.DefaultBannedRights.ToArray();
            if (info.Flags[2]) reactions = info.AvailableReactions.ToArray();
            if (info.Flags[3]) reactionsLimit = info.ReactionsLimit;
            if (info.Flags[5])
            {
                migratedFrom = info.MigratedFromChatId;
                migratedMax = info.MigratedFromMaxId;
            }
        }
        var builder = ChatFullInfo.Builder().ChatId(channelId).About(about);
        if (pinned > 0) builder = builder.PinnedMsgId(pinned);
        if (defaultRights != null) builder = builder.DefaultBannedRights(defaultRights);
        if (reactions != null) builder = builder.AvailableReactions(reactions);
        if (reactionsLimit.HasValue) builder = builder.ReactionsLimit(reactionsLimit.Value);
        if (migratedFrom > 0)
        {
            builder = builder.MigratedFromChatId(migratedFrom)
                .MigratedFromMaxId(migratedMax);
        }
        if (tabs) builder = builder.ForumTabs(true);
        using TLChatFullInfo updated = builder.Build();
        _chatRepository.PutFullInfo(updated);
    }

    // The full wire photo# row for a compact-row chatPhoto id, or null when the
    // chat/channel has no photo or the stored row is missing/malformed.
    public byte[]? GetStoredPhotoBytes(long? photoId)
    {
        if (photoId == null)
        {
            return null;
        }
        using TLBytes? photo = _photoRepository.GetPhoto(photoId.Value);
        if (photo == null ||
            ((Photo)photo.Value.AsSpan()).Constructor != Constructors.baseLayer_Photo)
        {
            return null;
        }
        return photo.Value.AsSpan().ToArray();
    }
}
