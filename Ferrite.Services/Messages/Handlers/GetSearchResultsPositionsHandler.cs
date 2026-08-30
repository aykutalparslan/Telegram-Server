// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetSearchResultsPositionsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly MessageSearchService _search;

    public GetSearchResultsPositionsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        MessageSearchService search)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _search = search;
    }

    [TLFunction(Constructors.baseLayer_GetSearchResultsPositions)]
    public async Task<TLSearchResultsPositions> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return (TLSearchResultsPositions)RpcErrorGenerator.GenerateError(401,
                    "AUTH_KEY_INVALID"u8);
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        var request = (GetSearchResultsPositions)q;
        MessageSearchTarget target = request.Flags[2]
            ? MessageSearchService.ResolveTarget(request.Get_SavedPeerIdView(),
                userId)
            : MessageSearchService.ResolveTarget(request.Get_PeerView(), userId);
        (TLMessagesFilter.MessagesFilterType filter, bool missedOnly) =
            MessageSearchService.ReadFilter(request.Get_FilterView());
        MessageSearchFilter.Criteria criteria = new MessageSearchFilter.Criteria
        {
            Filter = filter,
            MissedCallsOnly = missedOnly,
            ViewerUserId = userId,
        };
        int offsetId = request.OffsetId;
        int limit = request.Limit;

        (string? error, List<MessageSnapshot> matched) = await _search.SelectPeerAsync(
            userId, target, criteria);
        if (error != null)
        {
            return (TLSearchResultsPositions)RpcErrorGenerator.GenerateError(400,
                Encoding.UTF8.GetBytes(error));
        }

        var positions = new Vector();
        int emitted = 0;
        for (int index = 0; index < matched.Count; index++)
        {
            MessageSnapshot snapshot = matched[index];
            if (offsetId > 0 && snapshot.Id >= offsetId)
            {
                continue;
            }
            if (limit > 0 && emitted >= limit)
            {
                break;
            }
            using TLSearchResultsPosition position = SearchResultPosition.Builder()
                .MsgId(snapshot.Id)
                .Date(snapshot.Date)
                .Offset(index)
                .Build();
            positions.AppendTLObject(position.AsSpan());
            emitted++;
        }

        return SearchResultsPositions.Builder()
            .Count(matched.Count)
            .Positions(positions)
            .Build();
    }
}
