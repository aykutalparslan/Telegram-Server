// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

/// <summary>
/// Ferrite's public-post search policy. There is no paid search here, so every
/// query is free: the reported allowance never decreases, never imposes a wait
/// and never costs stars.
///
/// The row is not optional decoration. Pinned TDLib's `SearchPublicPostsQuery`
/// reads `search_flood` out of the `messages.messagesSlice` it receives and fails
/// the whole request with a fabricated `500 Failed to receive search limits` when
/// it is absent (`MessageQueryManager.cpp:578-584`), so every `channels.searchPosts`
/// answer carries it.
/// </summary>
public static class PublicPostSearchPolicy
{
    /// <summary>
    /// The daily allowance reported to clients. It is effectively unlimited while
    /// still being a real number: a zero `remains` is how pinned TDLib recognises
    /// an exhausted free quota.
    /// </summary>
    public const int FreeDailyQueries = 1_000_000;

    public static TLSearchPostsFlood BuildFlood() =>
        SearchPostsFlood.Builder()
            .QueryIsFree(true)
            .TotalDaily(FreeDailyQueries)
            .Remains(FreeDailyQueries)
            .StarsAmount(0)
            .Build();
}

/// <summary>
/// What a public-post search is looking for. Pinned TDLib sends exactly one of
/// the two: `td_api::searchPublicPosts` fills `query`, and
/// `td_api::searchPublicMessagesByTag` fills `hashtag` with its `#`/`$` prefix
/// still attached.
/// </summary>
public readonly record struct PublicPostQuery(string? Hashtag, string? Text);

/// <summary>
/// Searches the posts of public broadcast channels, which is the one search that
/// reaches past every box the caller owns or participates in. That makes it the
/// message index's real job: the index answers WHICH channels carry a match, and
/// each named channel is then read from its authoritative shared box, so the
/// result is durable state rather than an index document.
///
/// Only channel DISCOVERY is capped by the candidate limit; once a channel is
/// discovered every matching post in it is returned, because the box is read
/// whole.
/// </summary>
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

    /// <summary>
    /// Every matching public post, newest first, in the same total order global
    /// search uses so the same tuple pagination applies.
    /// </summary>
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

    /// <summary>
    /// The text handed to the index. A hashtag arrives with its `#`/`$` marker,
    /// but the index analyses a body into word terms, so the marker would match
    /// nothing; the exact tag boundary is enforced afterwards against the stored
    /// row by <see cref="MessageSearchFilter"/>.
    /// </summary>
    private static string? IndexText(PublicPostQuery query) =>
        query.Hashtag is { } hashtag
            ? MessageSearchFilter.StripHashtagMarker(hashtag)
            : query.Text;

    /// <summary>
    /// A post is public when it lives in a broadcast channel that published a
    /// username. Membership is deliberately NOT required -- that is what makes
    /// the post public -- but a caller the channel banned does not get to read it
    /// back through search.
    /// </summary>
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
