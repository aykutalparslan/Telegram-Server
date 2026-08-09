// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

/// <summary>
/// Searches one conversation the caller can read. Every criterion is evaluated
/// against the AUTHORITATIVE stored row, and the surviving rows keep their stored
/// newest-first order, so the page is cut by the same anchor/add-offset
/// pagination as plain history.
///
/// `max_id`/`min_id` are EXCLUSIVE bounds here, unlike the inclusive bounds plain
/// history uses, so they are owned by the predicate and deliberately not repeated
/// in the pagination query.
/// </summary>
public sealed class MessagesSearchHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly MessageSearchService _search;
    private readonly DialogBuilder _dialogs;

    public MessagesSearchHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, MessageSearchService search,
        DialogBuilder dialogs)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _search = search;
        _dialogs = dialogs;
    }

    [TLFunction(Constructors.baseLayer_MessagesSearch)]
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

        var request = (MessagesSearch)q;
        // A Saved Messages topic is addressed by saved_peer_id; Ferrite models
        // such a topic as the caller's own conversation with that peer, which
        // is exactly what messages.getSavedHistory reads.
        MessageSearchTarget target = request.Flags[2]
            ? MessageSearchService.ResolveTarget(request.Get_SavedPeerIdView(),
                userId)
            : MessageSearchService.ResolveTarget(request.Get_PeerView(), userId);
        (TLMessagesFilter.MessagesFilterType filter, bool missedOnly) =
            MessageSearchService.ReadFilter(request.Get_FilterView());
        (TLPeer.PeerType? fromType, long? fromId) = ResolveSender(request, userId);
        MessageSearchFilter.Criteria criteria = new MessageSearchFilter.Criteria
        {
            Filter = filter,
            MissedCallsOnly = missedOnly,
            Text = Encoding.UTF8.GetString(request.Q),
            FromPeerType = fromType,
            FromPeerId = fromId,
            TopMsgId = request.Flags[1] ? request.TopMsgId : null,
            MinDate = request.MinDate,
            MaxDate = request.MaxDate,
            MinId = request.MinId,
            MaxId = request.MaxId,
            ViewerUserId = userId,
        };
        HistoryQuery query = new HistoryQuery(request.OffsetId, 0, request.AddOffset,
            request.Limit, 0, 0);
        bool reactionTagged = request.Flags[3];

        // A saved-reaction tag search names Saved Messages tags, which Ferrite does
        // not model: no stored row carries one, so the truthful answer is an empty
        // result rather than every message in the conversation.
        if (reactionTagged)
        {
            return await BuildAsync(userId, target, [], query);
        }

        (string? error, List<MessageSnapshot> matched) = await _search.SelectPeerAsync(
            userId, target, criteria);
        if (error != null)
        {
            return (TLMessages)RpcErrorGenerator.GenerateError(400,
                Encoding.UTF8.GetBytes(error));
        }
        return await BuildAsync(userId, target, matched, query);
    }

    private Task<TLMessages> BuildAsync(long userId, MessageSearchTarget target,
        IReadOnlyList<MessageSnapshot> matched, HistoryQuery query) =>
        target.IsChannel
            ? _dialogs.BuildChannelSearchSliceAsync(userId, target.ChannelId, matched,
                query, "Search")
            : _dialogs.BuildCommonSearchSliceAsync(userId, target.PeerType,
                target.PeerId, matched, query, "Search");

    private static (TLPeer.PeerType? Type, long? Id) ResolveSender(
        MessagesSearch request, long userId)
    {
        if (!request.Flags[0])
        {
            return (null, null);
        }
        MessageSearchTarget sender = MessageSearchService.ResolveTarget(
            request.Get_FromIdView(), userId);
        return sender.IsChannel
            ? (TLPeer.PeerType.PeerChannel, sender.ChannelId)
            : (sender.PeerType, sender.PeerId);
    }
}
