// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Channels;

public static class ChannelRows
{
    public static async Task<byte[]> ForViewerAsync(IChatParticipantsRepository participants,
        long viewerUserId, long channelId, byte[] channelBytes)
    {
        var participant = await participants.GetParticipantAsync(channelId, viewerUserId);
        bool active = false;
        bool creator = false;
        if (participant != null)
        {
            int role = participant.Value.AsChatParticipantInfo().Role;
            active = role != (int)ChatParticipantRole.Banned &&
                     role != (int)ChatParticipantRole.Left;
            creator = role == (int)ChatParticipantRole.Creator;
            participant.Value.Dispose();
        }

        return ForViewer(channelBytes, active, creator);
    }

    public static byte[] ForViewer(byte[] channelBytes, bool viewerIsActiveParticipant,
        bool viewerIsCreator)
    {
        if (viewerIsActiveParticipant && viewerIsCreator)
        {
            return channelBytes;
        }

        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        if (stored.Type != TLChat.ChatType.Channel)
        {
            return channelBytes;
        }

        var channel = stored.AsChannel();
        Flags flags = channel.Flags;
        flags[0] = viewerIsCreator && channel.Creator;
        flags[2] = !viewerIsActiveParticipant;

        using TLChat adjusted = WithFlags(channel, flags, channel.Flags2);
        return adjusted.AsSpan().ToArray();
    }

    public static TLChat WithFlags(Channel source, Flags flags, Flags flags2) =>
        Rebuild(source, flags, flags2, source.Username, source.Usernames,
            source.Color, source.ProfileColor, source.EmojiStatus);

    public static TLChat WithUsernameCollection(Channel source,
        ReadOnlySpan<byte> username, Vector usernames)
    {
        Flags flags = source.Flags;
        flags[6] = username.Length > 0;
        Flags flags2 = source.Flags2;
        flags2[0] = usernames.Count > 0;
        return Rebuild(source, flags, flags2, username, usernames,
            source.Color, source.ProfileColor, source.EmojiStatus);
    }

    public static TLChat WithColor(Channel source, bool forProfile,
        ReadOnlySpan<byte> color)
    {
        Flags flags2 = source.Flags2;
        flags2[forProfile ? 8 : 7] = color.Length > 0;
        return Rebuild(source, source.Flags, flags2, source.Username,
            source.Usernames,
            forProfile ? source.Color : color,
            forProfile ? color : source.ProfileColor,
            source.EmojiStatus);
    }

    public static TLChat WithEmojiStatus(Channel source, ReadOnlySpan<byte> emojiStatus)
    {
        Flags flags2 = source.Flags2;
        flags2[9] = emojiStatus.Length > 0;
        return Rebuild(source, source.Flags, flags2, source.Username,
            source.Usernames, source.Color, source.ProfileColor, emojiStatus);
    }

    private static TLChat Rebuild(Channel source, Flags flags, Flags flags2,
        ReadOnlySpan<byte> username, Vector usernames, ReadOnlySpan<byte> color,
        ReadOnlySpan<byte> profileColor, ReadOnlySpan<byte> emojiStatus) =>
        new Channel(flags, flags[0], flags[2], flags[5], flags[7], flags[8],
            flags[9], flags[11], flags[12], flags[19], flags[20], flags[21],
            flags[22], flags[23], flags[24], flags[25], flags[26], flags[27],
            flags[28], flags[29], flags[30],
            flags2, flags2[1], flags2[2], flags2[3], flags2[12], flags2[15],
            flags2[16], flags2[17], flags2[19],
            source.Id, source.AccessHash, source.Title, username,
            source.Photo, source.Date, source.RestrictionReason,
            source.AdminRights, source.BannedRights, source.DefaultBannedRights,
            source.ParticipantsCount, usernames, source.StoriesMaxId,
            color, profileColor, emojiStatus, source.Level,
            source.SubscriptionUntilDate, source.BotVerificationIcon,
            source.SendPaidMessagesStars, source.LinkedMonoforumId);
}
