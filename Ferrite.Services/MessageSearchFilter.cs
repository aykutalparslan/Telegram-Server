// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services;

/// <summary>
/// The layer-214 search predicate, evaluated against the AUTHORITATIVE stored
/// row rather than against a search-index document. The index only ever narrows
/// candidates; every decision here reads the real <c>Message</c>, so a stale or
/// since-edited index entry can never put a row into a result.
/// </summary>
public static class MessageSearchFilter
{
    /// <summary>
    /// Everything the predicate needs, already resolved to scalars. Request views
    /// are ref structs and cannot cross an await, so a handler resolves its
    /// criteria first and then does its async work.
    /// </summary>
    public sealed record Criteria
    {
        public TLMessagesFilter.MessagesFilterType Filter { get; init; } =
            TLMessagesFilter.MessagesFilterType.InputMessagesFilterEmpty;
        public bool MissedCallsOnly { get; init; }
        public string? Text { get; init; }
        /// <summary>
        /// A whole-tag query, marker included (`#ferrite`, `$tsla`). Unlike
        /// <see cref="Text"/> it is not a substring test: `#cat` must not match
        /// `#category`.
        /// </summary>
        public string? Hashtag { get; init; }
        public TLPeer.PeerType? FromPeerType { get; init; }
        public long? FromPeerId { get; init; }
        public int? TopMsgId { get; init; }
        public int MinDate { get; init; }
        public int MaxDate { get; init; }
        public int MinId { get; init; }
        public int MaxId { get; init; }
        public bool OutgoingOnly { get; init; }
        /// <summary>The viewer, needed by the my-mentions filter.</summary>
        public long ViewerUserId { get; init; }
    }

    /// <summary>
    /// Narrows an already newest-first conversation, preserving its order so the
    /// caller can hand the result straight back to the ordinary history
    /// pagination and peer hydration.
    /// </summary>
    public static List<MessageSnapshot> Select(
        IReadOnlyList<MessageSnapshot> conversation, Criteria criteria)
    {
        var matched = new List<MessageSnapshot>();
        foreach (MessageSnapshot snapshot in conversation)
        {
            if (Matches(snapshot, criteria))
            {
                matched.Add(snapshot);
            }
        }
        return matched;
    }

    public static bool Matches(MessageSnapshot snapshot, Criteria criteria)
    {
        if (criteria.MinId > 0 && snapshot.Id <= criteria.MinId)
        {
            return false;
        }
        if (criteria.MaxId > 0 && snapshot.Id >= criteria.MaxId)
        {
            return false;
        }
        if (criteria.MinDate > 0 && snapshot.Date < criteria.MinDate)
        {
            return false;
        }
        if (criteria.MaxDate > 0 && snapshot.Date > criteria.MaxDate)
        {
            return false;
        }

        byte[] bytes = snapshot.Bytes;
        using var stored = new TLMessage(bytes, 0, bytes.Length);
        return stored.Type switch
        {
            TLMessage.MessageType.Message => MatchesMessage(stored.AsMessage(),
                criteria),
            TLMessage.MessageType.MessageService => MatchesService(
                stored.AsMessageService(), criteria),
            _ => false,
        };
    }

    private static bool MatchesMessage(Message message, Criteria criteria)
    {
        // A service-only filter can never be satisfied by an ordinary message.
        if (criteria.Filter is TLMessagesFilter.MessagesFilterType.InputMessagesFilterChatPhotos
            or TLMessagesFilter.MessagesFilterType.InputMessagesFilterPhoneCalls)
        {
            return false;
        }
        if (criteria.OutgoingOnly && !message.Flags[1])
        {
            return false;
        }
        if (!MatchesText(message.MessageProperty, criteria.Text))
        {
            return false;
        }
        if (!MatchesHashtag(message.MessageProperty, criteria.Hashtag))
        {
            return false;
        }
        if (!MatchesSender(message, criteria))
        {
            return false;
        }
        if (!MatchesTopMessage(message, criteria))
        {
            return false;
        }
        return MatchesFilter(message, criteria);
    }

    private static bool MatchesService(MessageService message, Criteria criteria)
    {
        // Service rows carry no body, so a text or tag query excludes them
        // outright.
        if (!string.IsNullOrEmpty(criteria.Text) ||
            !string.IsNullOrEmpty(criteria.Hashtag))
        {
            return false;
        }
        if (criteria.OutgoingOnly && !message.Flags[1])
        {
            return false;
        }
        if (!MatchesServiceSender(message, criteria))
        {
            return false;
        }

        var action = message.Get_ActionView();
        return criteria.Filter switch
        {
            TLMessagesFilter.MessagesFilterType.InputMessagesFilterEmpty => true,
            TLMessagesFilter.MessagesFilterType.InputMessagesFilterChatPhotos =>
                action.Is(out MessageActionChatEditPhoto _),
            TLMessagesFilter.MessagesFilterType.InputMessagesFilterPhoneCalls =>
                MatchesPhoneCall(action, criteria.MissedCallsOnly),
            TLMessagesFilter.MessagesFilterType.InputMessagesFilterMyMentions =>
                message.Flags[4],
            TLMessagesFilter.MessagesFilterType.InputMessagesFilterPinned =>
                message.Flags[24],
            _ => false,
        };
    }

    private static bool MatchesPhoneCall(MessageActionView action, bool missedOnly)
    {
        if (!action.Is(out MessageActionPhoneCall call))
        {
            return false;
        }
        if (!missedOnly)
        {
            return true;
        }
        // A missed call is the one whose discard reason says so; flags.0 gates
        // the reason, so an absent reason is never a missed call.
        return call.Flags[0] &&
               call.Get_ReasonView().Is(out PhoneCallDiscardReasonMissed _);
    }

    private static bool MatchesFilter(Message message, Criteria criteria)
    {
        switch (criteria.Filter)
        {
            case TLMessagesFilter.MessagesFilterType.InputMessagesFilterEmpty:
                return true;
            case TLMessagesFilter.MessagesFilterType.InputMessagesFilterMyMentions:
                return message.Flags[4];
            case TLMessagesFilter.MessagesFilterType.InputMessagesFilterPinned:
                return message.Flags[24];
            case TLMessagesFilter.MessagesFilterType.InputMessagesFilterUrl:
                return HasUrl(message);
        }

        if (!message.Flags[9])
        {
            return false;
        }

        var media = message.Get_MediaView();
        return criteria.Filter switch
        {
            TLMessagesFilter.MessagesFilterType.InputMessagesFilterPhotos =>
                media.Is(out MessageMediaPhoto _),
            TLMessagesFilter.MessagesFilterType.InputMessagesFilterVideo =>
                IsVideo(media, roundOnly: false),
            TLMessagesFilter.MessagesFilterType.InputMessagesFilterPhotoVideo =>
                media.Is(out MessageMediaPhoto _) || IsVideo(media, roundOnly: false),
            TLMessagesFilter.MessagesFilterType.InputMessagesFilterDocument =>
                IsPlainDocument(media),
            TLMessagesFilter.MessagesFilterType.InputMessagesFilterGif =>
                IsGif(media),
            TLMessagesFilter.MessagesFilterType.InputMessagesFilterVoice =>
                IsVoice(media),
            TLMessagesFilter.MessagesFilterType.InputMessagesFilterMusic =>
                IsMusic(media),
            // Round voice is the "voice message" tab: round video notes AND
            // voice notes both belong to it.
            TLMessagesFilter.MessagesFilterType.InputMessagesFilterRoundVoice =>
                IsVoice(media) || IsVideo(media, roundOnly: true),
            TLMessagesFilter.MessagesFilterType.InputMessagesFilterRoundVideo =>
                IsVideo(media, roundOnly: true),
            TLMessagesFilter.MessagesFilterType.InputMessagesFilterGeo =>
                media.Is(out MessageMediaGeo _) || media.Is(out MessageMediaGeoLive _) ||
                media.Is(out MessageMediaVenue _),
            TLMessagesFilter.MessagesFilterType.InputMessagesFilterContacts =>
                media.Is(out MessageMediaContact _),
            _ => false,
        };
    }

    private static bool IsVideo(MessageMediaView media, bool roundOnly)
    {
        if (!media.Is(out MessageMediaDocument document))
        {
            return false;
        }
        // Layer 214 carries video/round/voice as bare flags on the media itself,
        // so the common case needs no document inspection at all.
        if (document.Flags[6])
        {
            return document.Flags[7] == roundOnly;
        }
        if (document.Flags[7])
        {
            return roundOnly;
        }
        if (document.Flags[8] || !document.Flags[0])
        {
            return false;
        }
        return HasVideoAttribute(document, roundOnly);
    }

    private static bool IsVoice(MessageMediaView media)
    {
        if (!media.Is(out MessageMediaDocument document))
        {
            return false;
        }
        if (document.Flags[8])
        {
            return true;
        }
        if (document.Flags[6] || document.Flags[7] || !document.Flags[0])
        {
            return false;
        }
        return HasAudioAttribute(document, voiceOnly: true);
    }

    private static bool IsMusic(MessageMediaView media)
    {
        if (!media.Is(out MessageMediaDocument document))
        {
            return false;
        }
        if (document.Flags[8] || document.Flags[6] || document.Flags[7] ||
            !document.Flags[0])
        {
            return false;
        }
        return HasAudioAttribute(document, voiceOnly: false);
    }

    private static bool IsGif(MessageMediaView media)
    {
        if (!media.Is(out MessageMediaDocument document) || !document.Flags[0])
        {
            return false;
        }
        using TLDocument owned = document.Get_Document();
        if (owned.Type != TLDocument.DocumentType.Document)
        {
            return false;
        }
        var doc = owned.AsDocument();
        Vector attributes = doc.Attributes;
        int count = attributes.Count;
        for (int i = 0; i < count; i++)
        {
            var attribute = (DocumentAttributeView)attributes.ReadTLObject();
            if (attribute.Is(out DocumentAttributeAnimated _))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The document tab excludes everything that has its own tab: animations,
    /// audio, video, voice notes and stickers.
    /// </summary>
    private static bool IsPlainDocument(MessageMediaView media)
    {
        if (!media.Is(out MessageMediaDocument document) || !document.Flags[0])
        {
            return false;
        }
        if (document.Flags[6] || document.Flags[7] || document.Flags[8])
        {
            return false;
        }
        using TLDocument owned = document.Get_Document();
        if (owned.Type != TLDocument.DocumentType.Document)
        {
            return false;
        }
        var doc = owned.AsDocument();
        Vector attributes = doc.Attributes;
        int count = attributes.Count;
        for (int i = 0; i < count; i++)
        {
            var attribute = (DocumentAttributeView)attributes.ReadTLObject();
            if (attribute.Is(out DocumentAttributeAnimated _) ||
                attribute.Is(out DocumentAttributeAudio _) ||
                attribute.Is(out DocumentAttributeVideo _) ||
                attribute.Is(out DocumentAttributeSticker _))
            {
                return false;
            }
        }
        return true;
    }

    private static bool HasVideoAttribute(MessageMediaDocument document, bool roundOnly)
    {
        using TLDocument owned = document.Get_Document();
        if (owned.Type != TLDocument.DocumentType.Document)
        {
            return false;
        }
        var doc = owned.AsDocument();
        Vector attributes = doc.Attributes;
        int count = attributes.Count;
        for (int i = 0; i < count; i++)
        {
            var attribute = (DocumentAttributeView)attributes.ReadTLObject();
            if (attribute.Is(out DocumentAttributeVideo video))
            {
                return video.Flags[0] == roundOnly;
            }
        }
        return false;
    }

    private static bool HasAudioAttribute(MessageMediaDocument document, bool voiceOnly)
    {
        using TLDocument owned = document.Get_Document();
        if (owned.Type != TLDocument.DocumentType.Document)
        {
            return false;
        }
        var doc = owned.AsDocument();
        Vector attributes = doc.Attributes;
        int count = attributes.Count;
        for (int i = 0; i < count; i++)
        {
            var attribute = (DocumentAttributeView)attributes.ReadTLObject();
            if (attribute.Is(out DocumentAttributeAudio audio))
            {
                return audio.Flags[10] == voiceOnly;
            }
        }
        return false;
    }

    private static bool HasUrl(Message message)
    {
        if (message.Flags[9] && message.Get_MediaView().Is(out MessageMediaWebPage _))
        {
            return true;
        }
        if (!message.Flags[7])
        {
            return false;
        }
        Vector entities = message.Entities;
        int count = entities.Count;
        for (int i = 0; i < count; i++)
        {
            var entity = (MessageEntityView)entities.ReadTLObject();
            if (entity.Is(out MessageEntityUrl _) ||
                entity.Is(out MessageEntityTextUrl _))
            {
                return true;
            }
        }
        return false;
    }

    private static bool MatchesText(ReadOnlySpan<byte> body, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }
        if (body.IsEmpty)
        {
            return false;
        }
        return Encoding.UTF8.GetString(body)
            .Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Drops a leading `#`/`$` marker. Pinned TDLib always sends the marker
    /// (`MessageQueryManager.cpp:2186`), while an index analyses a body into word
    /// terms that never carry it.
    /// </summary>
    public static string StripHashtagMarker(string hashtag) =>
        hashtag.Length > 0 && hashtag[0] is '#' or '$' ? hashtag[1..] : hashtag;

    /// <summary>
    /// A whole-tag match. Telegram hashtags are case-insensitive and end at the
    /// first character that cannot belong to a tag, so `#cat` matches `#cat!` but
    /// never `#category`.
    /// </summary>
    private static bool MatchesHashtag(ReadOnlySpan<byte> body, string? hashtag)
    {
        if (string.IsNullOrEmpty(hashtag))
        {
            return true;
        }
        string tag = StripHashtagMarker(hashtag);
        if (tag.Length == 0 || body.IsEmpty)
        {
            return false;
        }

        // The marker the caller asked for still has to be present in the body;
        // `$tsla` and `#tsla` are different tags.
        char marker = hashtag[0] is '#' or '$' ? hashtag[0] : '#';
        string text = Encoding.UTF8.GetString(body);
        for (int start = 0; start < text.Length;)
        {
            int at = text.IndexOf(marker, start);
            if (at < 0)
            {
                return false;
            }
            start = at + 1;
            if (at > 0 && IsTagCharacter(text[at - 1]))
            {
                continue;
            }
            int end = at + 1 + tag.Length;
            if (end <= text.Length &&
                text.AsSpan(at + 1, tag.Length)
                    .Equals(tag, StringComparison.OrdinalIgnoreCase) &&
                (end == text.Length || !IsTagCharacter(text[end])))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsTagCharacter(char value) =>
        char.IsLetterOrDigit(value) || value == '_';

    private static bool MatchesSender(Message message, Criteria criteria)
    {
        if (criteria.FromPeerId is not { } fromId)
        {
            return true;
        }
        if (!message.Flags[8])
        {
            return false;
        }
        return MatchesPeer(message.Get_FromIdView(), criteria.FromPeerType, fromId);
    }

    private static bool MatchesServiceSender(MessageService message, Criteria criteria)
    {
        if (criteria.FromPeerId is not { } fromId)
        {
            return true;
        }
        if (!message.Flags[8])
        {
            return false;
        }
        return MatchesPeer(message.Get_FromIdView(), criteria.FromPeerType, fromId);
    }

    private static bool MatchesPeer(PeerView peer, TLPeer.PeerType? expectedType,
        long expectedId)
    {
        if (peer.Is(out PeerUser user))
        {
            return (expectedType is null or TLPeer.PeerType.PeerUser) &&
                   user.UserId == expectedId;
        }
        if (peer.Is(out PeerChat chat))
        {
            return (expectedType is null or TLPeer.PeerType.PeerChat) &&
                   chat.ChatId == expectedId;
        }
        if (peer.Is(out PeerChannel channel))
        {
            return (expectedType is null or TLPeer.PeerType.PeerChannel) &&
                   channel.ChannelId == expectedId;
        }
        return false;
    }

    private static bool MatchesTopMessage(Message message, Criteria criteria)
    {
        if (criteria.TopMsgId is not { } topMsgId)
        {
            return true;
        }
        if (!message.Flags[3])
        {
            return false;
        }
        var replyTo = message.Get_ReplyToView();
        if (!replyTo.Is(out MessageReplyHeader header))
        {
            return false;
        }
        // A reply directly to the topic root carries no reply_to_top_id of its
        // own, so it names the root through reply_to_msg_id instead.
        if (header.Flags[1])
        {
            return header.ReplyToTopId == topMsgId;
        }
        return header.Flags[4] && header.ReplyToMsgId == topMsgId;
    }
}
