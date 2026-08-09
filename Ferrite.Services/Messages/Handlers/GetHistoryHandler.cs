// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetHistoryHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly DialogBuilder _dialogs;

    public GetHistoryHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, DialogBuilder dialogs)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _dialogs = dialogs;
    }

    [TLFunction(Constructors.baseLayer_GetHistory)]
    public async Task<TLMessages> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        long userId = auth.Value.AsAuthInfo().UserId;
        TLPeer.PeerType peerType = default;
        long peerId = 0;
        var request = (GetHistory)q;
        HistoryQuery query = new HistoryQuery(request.OffsetId, request.OffsetDate,
            request.AddOffset, request.Limit, request.MaxId, request.MinId);
        long channelId = PeerResolver.ResolveInputPeerChannelId(request.Get_PeerView());
        if (channelId <= 0)
        {
            (peerType, peerId) = PeerResolver.ResolveHistoryPeer(
                request.Get_PeerView(), userId);
        }
        if (channelId > 0)
        {
            return await _dialogs.GetChannelHistoryAsync(userId, channelId, query);
        }

        return await _dialogs.GetHistoryForPeerAsync(userId, peerType, peerId, query,
            "GetHistory");
    }
}
