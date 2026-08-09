// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class MarkDialogUnreadHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly DialogOrganizationStore _organization;

    public MarkDialogUnreadHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        DialogOrganizationStore organization)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _organization = organization;
    }

    [TLFunction(Constructors.baseLayer_MarkDialogUnread)]
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

        var request = (MarkDialogUnread)q;
        if (request.Flags[1])
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400,
                "SAVED_PEER_ID_INVALID"u8);
        }
        bool unread = request.Unread;
        if (!PeerResolver.TryResolveInputDialogPeerKey(request.Get_PeerView(),
                userId, out DialogPeerKey peer))
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400,
                "PEER_ID_INVALID"u8);
        }

        return await _organization.MarkUnreadAsync(authKeyId, userId, peer, unread);
    }
}
