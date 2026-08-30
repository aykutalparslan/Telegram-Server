// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class SearchGlobalHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IDialogOrganizationRepository _dialogOrganizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly MessageSearchService _search;
    private readonly DialogBuilder _dialogs;

    public SearchGlobalHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IDialogOrganizationRepository dialogOrganizationRepository, MessageSearchService search,
        DialogBuilder dialogs)
    {
        _authorizationRepository = authorizationRepository;
        _dialogOrganizationRepository = dialogOrganizationRepository;

        _unitOfWork = unitOfWork;
        _search = search;
        _dialogs = dialogs;
    }

    [TLFunction(Constructors.baseLayer_SearchGlobal)]
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

        var request = (SearchGlobal)q;
        GlobalSearchScope scope = new GlobalSearchScope(request.BroadcastsOnly, request.GroupsOnly,
            request.UsersOnly);
        (TLMessagesFilter.MessagesFilterType filter, bool missedOnly) =
            MessageSearchService.ReadFilter(request.Get_FilterView());
        MessageSearchFilter.Criteria criteria = new MessageSearchFilter.Criteria
        {
            Filter = filter,
            MissedCallsOnly = missedOnly,
            Text = Encoding.UTF8.GetString(request.Q),
            MinDate = request.MinDate,
            MaxDate = request.MaxDate,
            ViewerUserId = userId,
        };
        MessageSearchTarget offsetPeer = MessageSearchService.ResolveTarget(
            request.Get_OffsetPeerView(), userId);
        int offsetRate = request.OffsetRate;
        long offsetPeerId = offsetPeer.IsChannel ? offsetPeer.ChannelId
            : offsetPeer.PeerId;
        int offsetId = request.OffsetId;
        int limit = request.Limit;
        int folderId = request.Flags[0] ? request.FolderId : 0;
        if (folderId is not (0 or 1))
        {
            return (TLMessages)RpcErrorGenerator.GenerateError(400,
                "FOLDER_ID_INVALID"u8);
        }

        List<GlobalSearchMatch> matched = await _search.SelectGlobalAsync(userId,
            scope, criteria);
        Dictionary<DialogPeerKey, DialogOrganizationState> organization =
            await DialogOrganizationStore.ReadPeerStatesAsync(
                _dialogOrganizationRepository, userId);
        matched = matched.Where(match => organization.GetValueOrDefault(
                new DialogPeerKey(match.PeerType, match.PeerId),
                DialogOrganizationState.Default).FolderId == folderId)
            .ToList();
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
        return await _dialogs.BuildGlobalSearchSliceAsync(userId, selected,
            matched.Count, nextRate, "SearchGlobal");
    }
}
