// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class ReorderPinnedDialogsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly DialogOrganizationStore _organization;

    public ReorderPinnedDialogsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        DialogOrganizationStore organization)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _organization = organization;
    }

    [TLFunction(Constructors.baseLayer_ReorderPinnedDialogs)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
    {
        using TLAuthInfo? auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
        {
            return (TLBool)RpcErrorGenerator.GenerateError(401,
                "AUTH_KEY_INVALID"u8);
        }
        long userId = auth.Value.AsAuthInfo().UserId;

        var request = (ReorderPinnedDialogs)q;
        int folderId = request.FolderId;
        Vector order = request.Order;
        var peers = new List<DialogPeerKey>(order.Count);
        for (int i = 0; i < order.Count; i++)
        {
            InputDialogPeerView item = order.ReadTLObject();
            if (!PeerResolver.TryResolveInputDialogPeerKey(item, userId,
                    out DialogPeerKey peer))
            {
                return (TLBool)RpcErrorGenerator.GenerateError(400,
                    "PEER_ID_INVALID"u8);
            }
            peers.Add(peer);
        }

        return await _organization.ReorderPinnedAsync(authKeyId, userId, folderId,
            peers);
    }
}
