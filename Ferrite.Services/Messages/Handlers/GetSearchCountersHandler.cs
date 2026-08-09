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
/// How many messages of one conversation each requested filter matches. The
/// counts come from the SAME predicate the search itself uses, so a count can
/// never disagree with the page it describes.
///
/// One counter is answered per requested filter, in the requested order, echoing
/// the filter back: pinned TDLib asks one filter at a time and rejects a response
/// whose single entry does not carry the filter it sent
/// (`MessagesManager.cpp:1125-1130`).
/// </summary>
public sealed class GetSearchCountersHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly MessageSearchService _search;

    public GetSearchCountersHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        MessageSearchService search)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _search = search;
    }

    private readonly record struct RequestedFilter(byte[] Bytes,
        TLMessagesFilter.MessagesFilterType Type, bool MissedOnly);

    [TLFunction(Constructors.baseLayer_GetSearchCounters)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return RpcErrorGenerator.GenerateError(401, "AUTH_KEY_INVALID"u8);
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        var filters = new List<RequestedFilter>();
        var request = (GetSearchCounters)q;
        MessageSearchTarget target = request.Flags[2]
            ? MessageSearchService.ResolveTarget(request.Get_SavedPeerIdView(),
                userId)
            : MessageSearchService.ResolveTarget(request.Get_PeerView(), userId);
        int topMsgId = request.Flags[0] ? request.TopMsgId : 0;

        Vector requested = request.Filters;
        int count = requested.Count;
        for (int i = 0; i < count; i++)
        {
            Span<byte> bytes = requested.ReadTLObject();
            (TLMessagesFilter.MessagesFilterType type, bool missedOnly) =
                MessageSearchService.ReadFilter((MessagesFilterView)bytes);
            // The echoed filter has to outlive the request view, so it is
            // copied out rather than referenced.
            filters.Add(new RequestedFilter(bytes.ToArray(), type, missedOnly));
        }

        (string? error, List<MessageSnapshot> conversation) = await _search
            .ReadPeerConversationAsync(userId, target);
        if (error != null)
        {
            return RpcErrorGenerator.GenerateError(400, Encoding.UTF8.GetBytes(error));
        }

        var counters = new List<TLSearchCounter>();
        foreach (RequestedFilter filter in filters)
        {
            int matched = MessageSearchFilter.Select(conversation,
                new MessageSearchFilter.Criteria
                {
                    Filter = filter.Type,
                    MissedCallsOnly = filter.MissedOnly,
                    TopMsgId = topMsgId > 0 ? topMsgId : null,
                    ViewerUserId = userId,
                }).Count;
            counters.Add(SearchCounter.Builder().Filter(filter.Bytes)
                .Count(matched).Build());
        }
        return ToCounterVector(counters);
    }

    private static TLBytes ToCounterVector(List<TLSearchCounter> counters)
    {
        var vector = new Vector();
        foreach (TLSearchCounter counter in counters)
        {
            vector.AppendTLObject(counter.AsSpan());
            counter.Dispose();
        }
        byte[] bytes = vector.ToReadOnlySpan().ToArray();
        return new TLBytes(bytes, 0, bytes.Length);
    }
}
