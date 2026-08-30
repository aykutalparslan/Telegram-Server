// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Messages;

public readonly record struct MessageSearchTarget(long ChannelId,
    TLPeer.PeerType PeerType, long PeerId)
{
    public bool IsChannel => ChannelId > 0;
}

public readonly record struct GlobalSearchScope(bool BroadcastsOnly, bool GroupsOnly,
    bool UsersOnly, bool OwnBoxOnly = false)
{
    public bool AllowsUsers => !BroadcastsOnly && !GroupsOnly;
    public bool AllowsBasicGroups => !BroadcastsOnly && !UsersOnly;
    public bool AllowsMegagroups => !OwnBoxOnly && !BroadcastsOnly && !UsersOnly;
    public bool AllowsBroadcasts => !OwnBoxOnly && !GroupsOnly && !UsersOnly;
}

public readonly record struct GlobalSearchMatch(MessageSnapshot Snapshot,
    TLPeer.PeerType PeerType, long PeerId);

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

    public static (TLMessagesFilter.MessagesFilterType Filter, bool MissedOnly)
        ReadFilter(MessagesFilterView filter)
    {
        TLMessagesFilter.MessagesFilterType type = filter.Type;
        bool missedOnly = filter.Is(out InputMessagesFilterPhoneCalls calls) &&
                          calls.Flags[0];
        return (type, missedOnly);
    }

    public async Task<(string? Error, List<MessageSnapshot> Matched)> SelectPeerAsync(
        long userId, MessageSearchTarget target, MessageSearchFilter.Criteria criteria)
    {
        (string? error, List<MessageSnapshot> conversation) =
            await ReadPeerConversationAsync(userId, target);
        return error != null
            ? (error, [])
            : (null, MessageSearchFilter.Select(conversation, criteria));
    }

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
