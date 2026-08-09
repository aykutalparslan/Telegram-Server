// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services;

// The stored compact channel row persists the creator's perspective (creator:true),
// but `creator` and `left` are per-VIEWER facts: they say what THIS account is to
// the channel. Serving the row unchanged tells every reader it owns the channel --
// TDLib silently no-ops joinChat on a discovered public channel for a
// non-participant, and `can_report_dialog` refuses a member's `reportChat` because
// `get_channel_status(...).is_creator()` is true (`DialogManager.cpp:2610`).
// So the row is adjusted per viewer: a non-participant sees left:true and no
// creator flag, an ordinary member sees neither, and only the real creator keeps
// the stored row. Per-viewer ADMIN/BANNED right hydration remains a recorded
// deferral; this covers creator and membership only.
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
        // A value-gated flag cannot be cleared through a builder, so the row goes
        // through the generated VALUE CONSTRUCTOR with an explicitly adjusted flag
        // word. Every flag the stored row carries is re-emitted; only `creator`
        // and `left` are rewritten, because only those two are per-viewer facts.
        Flags flags = channel.Flags;
        flags[0] = viewerIsCreator && channel.Creator;
        flags[2] = !viewerIsActiveParticipant;

        using TLChat adjusted = WithFlags(channel, flags, channel.Flags2);
        return adjusted.AsSpan().ToArray();
    }

    /// <summary>
    /// Rebuilds the row with a new flag word and every value it already carries.
    /// This is the shape every bare `flags.N?true` administration toggle needs:
    /// `signatures`, `signature_profiles`, `autotranslation`, `join_to_send`,
    /// `join_request`, `gigagroup`, `slowmode_enabled`, `has_geo`, `has_link`,
    /// `forum` and the rest.
    /// </summary>
    public static TLChat WithFlags(Channel source, Flags flags, Flags flags2) =>
        Rebuild(source, flags, flags2, source.Username, source.Usernames,
            source.Color, source.ProfileColor, source.EmojiStatus);

    /// <summary>
    /// Replaces BOTH username fields at once, which is the only safe way to
    /// write either: pinned TDLib discards a channel's whole username collection
    /// and logs an error when `username` and `usernames` are both non-empty
    /// (`Usernames.cpp:17-28`). <see cref="Channels.ChannelUsernames.Apply"/>
    /// is the caller that chooses which of the two forms a collection takes.
    /// </summary>
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

    /// <summary>
    /// Replaces the name or profile <c>PeerColor</c>; an empty value clears the
    /// one it names and leaves the other untouched.
    /// </summary>
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

    /// <summary>
    /// Replaces `emoji_status`; an `emojiStatusEmpty` request clears it, which is
    /// how a client removes a channel's status.
    /// </summary>
    public static TLChat WithEmojiStatus(Channel source, ReadOnlySpan<byte> emojiStatus)
    {
        Flags flags2 = source.Flags2;
        flags2[9] = emojiStatus.Length > 0;
        return Rebuild(source, source.Flags, flags2, source.Username,
            source.Usernames, source.Color, source.ProfileColor, emojiStatus);
    }

    // The one place where the full field list of `channel` is enumerated. Anything
    // added to the schema shows up here as a compile error rather than as a field
    // silently dropped from every rebuilt row. The flag word is authoritative: the
    // generated value constructor writes a field only when its flag is set, which
    // is exactly why clearing one has to come through here and not a builder.
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
