// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Services.Users;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Messages;

public static class MessageRows
{
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

    public static TLMessage RebuildMedia(Message source, ReadOnlySpan<byte> media)
    {
        Flags flags = source.Flags;
        flags[9] = true;
        return Rebuild(source, flags, source.Flags2, source.MessageProperty,
            source.Entities, media, source.ReplyMarkup, source.EditDate);
    }

    public static TLMessage RebuildTtl(Message source, int ttlPeriod)
    {
        Flags flags = source.Flags;
        flags[25] = ttlPeriod > 0;
        return Rebuild(source, flags, source.Flags2, source.MessageProperty,
            source.Entities, source.Media, source.ReplyMarkup, source.EditDate,
            ttlPeriod);
    }

    public static byte[] StampOutgoingForViewer(byte[] messageBytes, long viewerUserId)
    {
        using var stored = new TLMessage(messageBytes, 0, messageBytes.Length);
        if (stored.Type != TLMessage.MessageType.Message)
        {
            return messageBytes;
        }
        Message source = stored.AsMessage();
        if (source.OutProperty || !source.Flags[8])
        {
            return messageBytes;
        }
        if (!PeerResolver.TryReadPeer(source.Get_FromIdView(), out var from) ||
            from.Type != TLPeer.PeerType.PeerUser || from.Id != viewerUserId)
        {
            return messageBytes;
        }

        Flags flags = source.Flags;
        flags[1] = true;
        using TLMessage stamped = Rebuild(source, flags, source.Flags2,
            source.MessageProperty, source.Entities, source.Media,
            source.ReplyMarkup, source.EditDate);
        return stamped.AsSpan().ToArray();
    }

    private static TLMessage Rebuild(Message source, Flags flags, Flags flags2,
        ReadOnlySpan<byte> text, Vector entities, ReadOnlySpan<byte> media,
        ReadOnlySpan<byte> replyMarkup, int editDate, int? ttlPeriod = null) =>
        new Message(flags, flags[1], flags[4], flags[5], flags[13], flags[14],
            flags[18], flags[19], flags[21], flags[24], flags[26], flags[27],
            flags2, flags2[1], flags2[4], flags2[8], flags2[9],
            source.Id, source.FromId, source.FromBoostsApplied, source.FromRank,
            source.PeerId,
            source.SavedPeerId, source.FwdFrom, source.ViaBotId,
            source.ViaBusinessBotId, source.GuestchatViaFrom, source.ReplyTo,
            source.Date, text, media,
            replyMarkup, entities, source.Views, source.Forwards, source.Replies,
            editDate, source.PostAuthor, source.GroupedId, source.Reactions,
            source.RestrictionReason, ttlPeriod ?? source.TtlPeriod,
            source.QuickReplyShortcutId, source.Effect, source.Factcheck,
            source.ReportDeliveryUntilDate, source.PaidMessageStars,
            source.SuggestedPost, source.ScheduleRepeatPeriod,
            source.SummaryFromLanguage, source.RichMessage);
}
