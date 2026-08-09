// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

/// <summary>
/// The conversation a search runs over. A channel lives in one shared box and a
/// private/basic-group conversation in the caller's own box, so the two are
/// resolved once here and every search method reuses the answer.
/// </summary>
public readonly record struct MessageSearchTarget(long ChannelId,
    TLPeer.PeerType PeerType, long PeerId)
{
    public bool IsChannel => ChannelId > 0;
}

/// <summary>
/// Which conversations a cross-conversation search may look at. The three chat
/// type flags are the layer-214 ones and are mutually exclusive on the wire; none
/// set means every conversation the caller can read. <paramref name="OwnBoxOnly"/>
/// is not a wire field: it keeps a search that asks about the caller's OWN
/// messages out of shared channel boxes, where a post is not a per-viewer copy.
/// </summary>
public readonly record struct GlobalSearchScope(bool BroadcastsOnly, bool GroupsOnly,
    bool UsersOnly, bool OwnBoxOnly = false)
{
    public bool AllowsUsers => !BroadcastsOnly && !GroupsOnly;
    public bool AllowsBasicGroups => !BroadcastsOnly && !UsersOnly;
    public bool AllowsMegagroups => !OwnBoxOnly && !BroadcastsOnly && !UsersOnly;
    public bool AllowsBroadcasts => !OwnBoxOnly && !GroupsOnly && !UsersOnly;
}

/// <summary>One match of a search that spans conversations.</summary>
public readonly record struct GlobalSearchMatch(MessageSnapshot Snapshot,
    TLPeer.PeerType PeerType, long PeerId);

/// <summary>
/// Joins search candidates to the AUTHORITATIVE stored rows. The message index
/// only ever narrows what has to be looked at; every result here comes out of a
/// durable message box and is evaluated by <see cref="MessageSearchFilter"/>
/// against the real <c>Message</c>, so a stale or since-edited index entry can
/// never become protocol state.
///
/// Ferrite's message repositories read a whole box at once, so a box-scoped read
/// is already complete and needs no index narrowing; the index stays the
/// discovery mechanism for searches that reach beyond the caller's own boxes.
/// </summary>
public sealed class MessageSearchService
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    private readonly IChatRepository _chatRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly DialogBuilder _dialogs;

    public MessageSearchService(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, DialogBuilder dialogs)
    {
        _chatParticipantsRepository = chatParticipantsRepository;

        _chatRepository = chatRepository;

        _unitOfWork = unitOfWork;
        _dialogs = dialogs;
    }

    /// <summary>
    /// Resolves the requested peer. Ref-struct views cannot cross an await, so a
    /// handler calls this before its first asynchronous step.
    /// </summary>
    public static MessageSearchTarget ResolveTarget(InputPeerView peer,
        long selfUserId)
    {
        long channelId = PeerResolver.ResolveInputPeerChannelId(peer);
        if (channelId > 0)
        {
            return new MessageSearchTarget(channelId, TLPeer.PeerType.PeerChannel,
                channelId);
        }
        (TLPeer.PeerType peerType, long peerId) = PeerResolver.ResolveHistoryPeer(peer,
            selfUserId);
        return new MessageSearchTarget(0, peerType, peerId);
    }

    /// <summary>
    /// The layer-214 filter plus the one flag that is carried inside it: only
    /// <c>inputMessagesFilterPhoneCalls</c> narrows itself further, to missed
    /// calls.
    /// </summary>
    public static (TLMessagesFilter.MessagesFilterType Filter, bool MissedOnly)
        ReadFilter(MessagesFilterView filter)
    {
        TLMessagesFilter.MessagesFilterType type = filter.Type;
        bool missedOnly = filter.Is(out InputMessagesFilterPhoneCalls calls) &&
                          calls.Flags[0];
        return (type, missedOnly);
    }

    /// <summary>
    /// Every matching row of one conversation, newest first. The order is the
    /// stored order, so the caller can hand the result straight to the ordinary
    /// history pagination and peer hydration.
    /// </summary>
    public async Task<(string? Error, List<MessageSnapshot> Matched)> SelectPeerAsync(
        long userId, MessageSearchTarget target, MessageSearchFilter.Criteria criteria)
    {
        (string? error, List<MessageSnapshot> conversation) =
            await ReadPeerConversationAsync(userId, target);
        return error != null
            ? (error, [])
            : (null, MessageSearchFilter.Select(conversation, criteria));
    }

    /// <summary>
    /// The conversation itself, access-checked but unfiltered. Search metadata
    /// evaluates several predicates over one conversation -- counters run one per
    /// requested filter -- so it reads the box once and narrows it in memory.
    /// </summary>
    public async Task<(string? Error, List<MessageSnapshot> Conversation)>
        ReadPeerConversationAsync(long userId, MessageSearchTarget target)
    {
        if (target.IsChannel)
        {
            string? accessError = await ChannelAccess.ValidateReadAsync(_chatRepository, _chatParticipantsRepository, target.ChannelId, userId);
            if (accessError != null)
            {
                return (accessError, []);
            }

            return (null, await _dialogs.ReadChannelConversationAsync(
                target.ChannelId));
        }

        if (target.PeerId <= 0)
        {
            return ("PEER_ID_INVALID", []);
        }

        return (null, await _dialogs.ReadCommonConversationAsync(userId,
            target.PeerType, target.PeerId));
    }

    /// <summary>
    /// Every matching row the caller can read, across conversations, ordered
    /// newest first. The caller's own box covers private chats and basic groups;
    /// channels keep one shared box each, so an accessible channel is read from
    /// its own box rather than from a per-viewer copy that does not exist.
    /// </summary>
    public async Task<List<GlobalSearchMatch>> SelectGlobalAsync(long userId,
        GlobalSearchScope scope, MessageSearchFilter.Criteria criteria)
    {
        var matches = new List<GlobalSearchMatch>();

        List<BoxMessage> box = await _dialogs.ReadCommonBoxAsync(userId);
        foreach (BoxMessage row in box)
        {
            if (!AllowsCommonPeer(scope, row.PeerType))
            {
                continue;
            }
            if (MessageSearchFilter.Matches(row.Snapshot, criteria))
            {
                matches.Add(new GlobalSearchMatch(row.Snapshot, row.PeerType,
                    row.PeerId));
            }
        }

        foreach (long channelId in await ReadAccessibleChannelIdsAsync(userId, scope))
        {
            List<MessageSnapshot> posts = await _dialogs.ReadChannelConversationAsync(
                channelId);
            foreach (MessageSnapshot post in MessageSearchFilter.Select(posts, criteria))
            {
                matches.Add(new GlobalSearchMatch(post, TLPeer.PeerType.PeerChannel,
                    channelId));
            }
        }

        matches.Sort(CompareNewestFirst);
        return matches;
    }

    /// <summary>
    /// Global results are ordered by date, then by conversation, then by id.
    /// Pagination depends on this being a TOTAL order: the client resumes from
    /// the tuple of the last row it received, so two rows that compare equal
    /// would either repeat or vanish between pages.
    /// </summary>
    public static int CompareNewestFirst(GlobalSearchMatch left,
        GlobalSearchMatch right)
    {
        int byDate = right.Snapshot.Date.CompareTo(left.Snapshot.Date);
        if (byDate != 0)
        {
            return byDate;
        }
        int byPeer = right.PeerId.CompareTo(left.PeerId);
        return byPeer != 0 ? byPeer : right.Snapshot.Id.CompareTo(left.Snapshot.Id);
    }

    /// <summary>
    /// Drops everything up to and including the row the client last received.
    /// An offset naming no stored row still positions the page, because the
    /// comparison is on the tuple rather than on identity.
    ///
    /// The RATE anchors the tuple: it is 0 on a first page and thereafter the
    /// `next_rate` of the previous answer. An id without a rate is out of
    /// contract, and answering it from the top repeats a page, where treating it
    /// as an unanchored tuple would silently drop every remaining result.
    /// </summary>
    public static List<GlobalSearchMatch> ApplyGlobalOffset(
        IReadOnlyList<GlobalSearchMatch> ordered, int offsetRate, long offsetPeerId,
        int offsetId)
    {
        var page = new List<GlobalSearchMatch>();
        if (offsetRate <= 0)
        {
            page.AddRange(ordered);
            return page;
        }

        var offset = new GlobalSearchMatch(
            new MessageSnapshot(offsetId, offsetRate, []), TLPeer.PeerType.PeerUser,
            offsetPeerId);
        foreach (GlobalSearchMatch match in ordered)
        {
            if (CompareNewestFirst(offset, match) < 0)
            {
                page.Add(match);
            }
        }
        return page;
    }

    private static bool AllowsCommonPeer(GlobalSearchScope scope,
        TLPeer.PeerType peerType) => peerType switch
    {
        TLPeer.PeerType.PeerUser => scope.AllowsUsers,
        TLPeer.PeerType.PeerChat => scope.AllowsBasicGroups,
        // A channel post is never a per-viewer copy, so a channel row in the
        // caller's own box is not a conversation this search owns.
        _ => false,
    };

    private async Task<List<long>> ReadAccessibleChannelIdsAsync(long userId,
        GlobalSearchScope scope)
    {
        var channelIds = new List<long>();
        if (!scope.AllowsMegagroups && !scope.AllowsBroadcasts)
        {
            return channelIds;
        }

        IReadOnlyCollection<TLChatParticipantInfo> memberships = await _chatParticipantsRepository.GetParticipantsByUserAsync(userId);
        var candidates = new List<long>();
        foreach (TLChatParticipantInfo membership in memberships)
        {
            using (membership)
            {
                var info = membership.AsChatParticipantInfo();
                if (info.Role is (int)ChatParticipantRole.Banned
                    or (int)ChatParticipantRole.Left)
                {
                    continue;
                }
                candidates.Add(info.ChatId);
            }
        }

        foreach (long chatId in candidates.Distinct())
        {
            using TLChat? chat = await _chatRepository.GetChatAsync(chatId);
            if (chat == null || chat.Value.Type != TLChat.ChatType.Channel)
            {
                continue;
            }
            var channel = chat.Value.AsChannel();
            bool broadcast = channel.Flags[5];
            bool megagroup = channel.Flags[8];
            // A channel that claims neither shape is treated as a broadcast, which
            // is what the wire default means.
            bool allowed = megagroup && !broadcast
                ? scope.AllowsMegagroups
                : scope.AllowsBroadcasts;
            if (allowed)
            {
                channelIds.Add(chatId);
            }
        }
        return channelIds;
    }
}
