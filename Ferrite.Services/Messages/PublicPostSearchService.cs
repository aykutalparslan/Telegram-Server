// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Messages;

public static class PublicPostSearchPolicy
{
    public const int FreeDailyQueries = 1_000_000;

    public static TLSearchPostsFlood BuildFlood() =>
        SearchPostsFlood.Builder()
            .QueryIsFree(true)
            .TotalDaily(FreeDailyQueries)
            .Remains(FreeDailyQueries)
            .StarsAmount(0)
            .Build();
}

public readonly record struct PublicPostQuery(string? Hashtag, string? Text);

public sealed class PublicPostSearchService
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    private readonly IChatRepository _chatRepository;

    private const int ChannelDiscoveryCandidateLimit = 500;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ISearchEngine _search;
    private readonly DialogBuilder _dialogs;

    public PublicPostSearchService(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, ISearchEngine search,
        DialogBuilder dialogs)
    {
        _chatParticipantsRepository = chatParticipantsRepository;

        _chatRepository = chatRepository;

        _unitOfWork = unitOfWork;
        _search = search;
        _dialogs = dialogs;
    }

    public async Task<List<GlobalSearchMatch>> SearchAsync(long userId,
        PublicPostQuery query)
    {
        var criteria = new MessageSearchFilter.Criteria
        {
            Text = query.Text,
            Hashtag = query.Hashtag,
            ViewerUserId = userId,
        };

        var matches = new List<GlobalSearchMatch>();
        foreach (long channelId in await DiscoverChannelsAsync(userId, query))
        {
            List<MessageSnapshot> posts = await _dialogs.ReadChannelConversationAsync(
                channelId);
            foreach (MessageSnapshot post in MessageSearchFilter.Select(posts, criteria))
            {
                matches.Add(new GlobalSearchMatch(post, TLPeer.PeerType.PeerChannel,
                    channelId));
            }
        }

        matches.Sort(MessageSearchService.CompareNewestFirst);
        return matches;
    }

    private async Task<List<long>> DiscoverChannelsAsync(long userId,
        PublicPostQuery query)
    {
        List<MessageSearchModel> candidates = await _search.SearchMessageCandidates(
            new MessageCandidateQuery(null, (int)TLPeer.PeerType.PeerChannel, null,
                IndexText(query), ChannelDiscoveryCandidateLimit));

        var channelIds = new List<long>();
        var seen = new HashSet<long>();
        foreach (MessageSearchModel candidate in candidates)
        {
            if (!seen.Add(candidate.PeerId))
            {
                continue;
            }
            if (await IsPublicBroadcastReadableAsync(candidate.PeerId, userId))
            {
                channelIds.Add(candidate.PeerId);
            }
        }
        return channelIds;
    }

    private static string? IndexText(PublicPostQuery query) =>
        query.Hashtag is { } hashtag
            ? MessageSearchFilter.StripHashtagMarker(hashtag)
            : query.Text;

    private async Task<bool> IsPublicBroadcastReadableAsync(long channelId, long userId)
    {
        using (TLChat? chat = await _chatRepository.GetChatAsync(channelId))
        {
            if (chat == null || chat.Value.Type != TLChat.ChatType.Channel)
            {
                return false;
            }
            var channel = chat.Value.AsChannel();
            if (!channel.Flags[5] || !channel.Flags[6] || channel.Username.IsEmpty)
            {
                return false;
            }
        }

        using TLChatParticipantInfo? participant = await _chatParticipantsRepository.GetParticipantAsync(channelId, userId);
        return participant == null ||
               participant.Value.AsChatParticipantInfo().Role !=
                   (int)ChatParticipantRole.Banned;
    }
}
