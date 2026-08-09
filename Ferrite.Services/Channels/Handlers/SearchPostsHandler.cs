// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.Channels;

/// <summary>
/// Searches the posts of public broadcast channels, the one search that reaches
/// past every conversation the caller takes part in. Pinned TDLib drives it from
/// two places: `td_api::searchPublicPosts` fills `query`
/// (`MessageQueryManager.cpp:566`) and `td_api::searchPublicMessagesByTag` fills
/// `hashtag` (`:647`).
///
/// Paging is the same (date, peer, id) tuple global search uses, anchored by
/// `offset_rate`.
/// </summary>
public sealed class SearchPostsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly PublicPostSearchService _posts;
    private readonly DialogBuilder _dialogs;

    public SearchPostsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, PublicPostSearchService posts,
        DialogBuilder dialogs)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _posts = posts;
        _dialogs = dialogs;
    }

    [TLFunction(Constructors.baseLayer_ChannelsSearchPosts)]
    public async Task<TLMessages> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return (TLMessages)RpcErrorGenerator.GenerateError(401,
                    "AUTH_KEY_INVALID"u8);
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        var request = (ChannelsSearchPosts)q;
        // Pinned TDLib sets exactly one of the two, and the one it sets picks
        // the matching rule: a tag is matched whole, a query as free text.
        bool byHashtag = request.Flags[0];
        string term = Encoding.UTF8.GetString(byHashtag
            ? request.Hashtag
            : request.Query);
        MessageSearchTarget offsetPeer = MessageSearchService.ResolveTarget(
            request.Get_OffsetPeerView(), userId);
        int offsetRate = request.OffsetRate;
        long offsetPeerId = offsetPeer.IsChannel ? offsetPeer.ChannelId
            : offsetPeer.PeerId;
        int offsetId = request.OffsetId;
        int limit = request.Limit;

        // An empty term cannot select anything. Both errors are the ones pinned
        // TDLib turns into an empty result rather than surfacing to the user
        // (`MessageQueryManager.cpp:617,676`), so refusing here costs the client
        // nothing.
        if (byHashtag && MessageSearchFilter.StripHashtagMarker(term).Length == 0)
        {
            return (TLMessages)RpcErrorGenerator.GenerateError(400,
                "SEARCH_QUERY_EMPTY"u8);
        }
        if (!byHashtag && string.IsNullOrWhiteSpace(term))
        {
            return (TLMessages)RpcErrorGenerator.GenerateError(400, "QUERY_EMPTY"u8);
        }

        List<GlobalSearchMatch> matched = await _posts.SearchAsync(userId,
            byHashtag ? new PublicPostQuery(term, null)
                : new PublicPostQuery(null, term));
        List<GlobalSearchMatch> page = MessageSearchService.ApplyGlobalOffset(matched,
            offsetRate, offsetPeerId, offsetId);

        var selected = new List<byte[]>();
        foreach (GlobalSearchMatch match in page)
        {
            if (limit > 0 && selected.Count >= limit)
            {
                break;
            }
            selected.Add(match.Snapshot.Bytes);
        }

        int? nextRate = selected.Count > 0
            ? page[selected.Count - 1].Snapshot.Date
            : null;
        byte[] flood;
        using (TLSearchPostsFlood row = PublicPostSearchPolicy.BuildFlood())
        {
            // The row has to outlive the pooled value here, because the slice is
            // built after this method's remaining await.
            flood = row.AsSpan().ToArray();
        }
        return await _dialogs.BuildPublicPostSearchSliceAsync(userId, selected,
            matched.Count, nextRate, flood, "SearchPosts");
    }
}
