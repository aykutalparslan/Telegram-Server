// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class SearchSentMediaHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly MessageSearchService _search;
    private readonly DialogBuilder _dialogs;

    public SearchSentMediaHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, MessageSearchService search,
        DialogBuilder dialogs)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _search = search;
        _dialogs = dialogs;
    }

    [TLFunction(Constructors.baseLayer_SearchSentMedia)]
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

        var request = (SearchSentMedia)q;
        (TLMessagesFilter.MessagesFilterType filter, bool missedOnly) =
            MessageSearchService.ReadFilter(request.Get_FilterView());
        MessageSearchFilter.Criteria criteria = new MessageSearchFilter.Criteria
        {
            Filter = filter,
            MissedCallsOnly = missedOnly,
            Text = Encoding.UTF8.GetString(request.Q),
            OutgoingOnly = true,
            ViewerUserId = userId,
        };
        int limit = request.Limit;

        List<GlobalSearchMatch> matched = await _search.SelectGlobalAsync(userId,
            new GlobalSearchScope(false, false, false, OwnBoxOnly: true), criteria);

        var selected = new List<byte[]>();
        foreach (GlobalSearchMatch match in matched)
        {
            if (limit > 0 && selected.Count >= limit)
            {
                break;
            }
            selected.Add(match.Snapshot.Bytes);
        }

        return await _dialogs.BuildGlobalSearchSliceAsync(userId, selected,
            matched.Count, null, "SearchSentMedia");
    }
}
