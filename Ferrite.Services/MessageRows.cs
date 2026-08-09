// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services;

/// <summary>
/// Rebuilds a stored `message` row for an edit. A generated builder can SET a
/// flag that gates a value but never clear it, and editing text drops the old
/// entities, so the value constructor is used with an explicitly adjusted flag
/// word instead of <c>Clone()</c>. Every flag the source row carries is
/// re-emitted; only the fields an edit names may appear or disappear.
/// </summary>
public static class MessageRows
{
    /// <summary>
    /// Applies one edit to a single stored copy. The copy keeps its own local id,
    /// in/out perspective, peers, and every optional field the edit does not name.
    /// Entities travel with the text: replacing the text without supplying
    /// entities clears them, which is what a client expects after an edit.
    /// </summary>
    public static TLMessage RebuildEdited(Message source,
        ReadOnlySpan<byte> text, bool replaceText,
        Vector entities, bool replaceEntities,
        ReadOnlySpan<byte> media, bool replaceMedia,
        ReadOnlySpan<byte> replyMarkup, bool replaceReplyMarkup,
        int editDate)
    {
        Flags flags = source.Flags;
        Flags flags2 = source.Flags2;

        ReadOnlySpan<byte> newText = replaceText ? text : source.MessageProperty;

        Vector newEntities = source.Entities;
        if (replaceEntities)
        {
            newEntities = entities;
            flags[7] = true;
        }
        else if (replaceText)
        {
            newEntities = default;
            flags[7] = false;
        }

        ReadOnlySpan<byte> newMedia = source.Media;
        if (replaceMedia)
        {
            newMedia = media;
            flags[9] = true;
        }

        ReadOnlySpan<byte> newReplyMarkup = source.ReplyMarkup;
        if (replaceReplyMarkup)
        {
            newReplyMarkup = replyMarkup;
            flags[6] = true;
        }

        flags[15] = true;
        return Rebuild(source, flags, flags2, newText, newEntities, newMedia,
            newReplyMarkup, editDate);
    }

    /// <summary>
    /// Replaces only the media of a stored copy. Every other field, including
    /// <c>edit_date</c>, survives untouched: a poll vote rewrites the row's
    /// results but is not an edit, and stamping an edit date would make every
    /// client label a message nobody changed as edited.
    /// </summary>
    public static TLMessage RebuildMedia(Message source, ReadOnlySpan<byte> media)
    {
        Flags flags = source.Flags;
        flags[9] = true;
        return Rebuild(source, flags, source.Flags2, source.MessageProperty,
            source.Entities, media, source.ReplyMarkup, source.EditDate);
    }

    /// <summary>
    /// Replaces the auto-delete period of a stored copy, clearing it when the
    /// destination conversation has no timer. A forwarded row must not inherit the
    /// source dialog's `ttl_period`, and clearing a value-gated flag is exactly
    /// what a builder cannot express.
    /// </summary>
    public static TLMessage RebuildTtl(Message source, int ttlPeriod)
    {
        Flags flags = source.Flags;
        flags[25] = ttlPeriod > 0;
        return Rebuild(source, flags, source.Flags2, source.MessageProperty,
            source.Entities, source.Media, source.ReplyMarkup, source.EditDate,
            ttlPeriod);
    }

    // One place where the full field list of `message` is enumerated. Anything
    // added to the schema shows up here as a compile error rather than as a
    // field silently dropped from every edited row.
    private static TLMessage Rebuild(Message source, Flags flags, Flags flags2,
        ReadOnlySpan<byte> text, Vector entities, ReadOnlySpan<byte> media,
        ReadOnlySpan<byte> replyMarkup, int editDate, int? ttlPeriod = null) =>
        new Message(flags, flags[1], flags[4], flags[5], flags[13], flags[14],
            flags[18], flags[19], flags[21], flags[24], flags[26], flags[27],
            flags2, flags2[1], flags2[4], flags2[8], flags2[9],
            source.Id, source.FromId, source.FromBoostsApplied, source.PeerId,
            source.SavedPeerId, source.FwdFrom, source.ViaBotId,
            source.ViaBusinessBotId, source.ReplyTo, source.Date, text, media,
            replyMarkup, entities, source.Views, source.Forwards, source.Replies,
            editDate, source.PostAuthor, source.GroupedId, source.Reactions,
            source.RestrictionReason, ttlPeriod ?? source.TtlPeriod,
            source.QuickReplyShortcutId, source.Effect, source.Factcheck,
            source.ReportDeliveryUntilDate, source.PaidMessageStars,
            source.SuggestedPost);
}
