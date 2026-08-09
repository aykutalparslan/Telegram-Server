// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class ToggleDialogPinHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly DialogOrganizationStore _organization;

    public ToggleDialogPinHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        DialogOrganizationStore organization)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _organization = organization;
    }

    [TLFunction(Constructors.baseLayer_ToggleDialogPin)]
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

        var request = (ToggleDialogPin)q;
        bool pinned = request.Pinned;
        if (!PeerResolver.TryResolveInputDialogPeerKey(request.Get_PeerView(),
                userId, out DialogPeerKey peer))
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400,
                "PEER_ID_INVALID"u8);
        }

        return await _organization.TogglePinAsync(authKeyId, userId, peer, pinned);
    }
}
