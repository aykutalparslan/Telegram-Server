// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.contacts;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.ContactMethods;

/// <summary>
/// Clears the caller's interaction rating for one peer in one category. Pinned
/// TDLib sends this from `td_api::removeTopChat`
/// (`TopDialogManager.cpp:105`).
///
/// Ferrite does not score interactions yet, so there is never a stored rating to
/// remove. The call still validates the peer and answers true, because removing
/// an absent rating is genuinely idempotent -- reporting an error would make a
/// client retry something that is already in the requested state.
/// </summary>
public sealed class ResetTopPeerRatingHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;

    public ResetTopPeerRatingHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository) {
        _authorizationRepository = authorizationRepository;
        _unitOfWork = unitOfWork;
    }

    [TLFunction(Constructors.baseLayer_ResetTopPeerRating)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return (TLBool)RpcErrorGenerator.GenerateError(401, "AUTH_KEY_INVALID"u8);
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        var request = (ResetTopPeerRating)q;
        (TLPeer.PeerType peerType, long peerId) = PeerResolver.ResolveHistoryPeer(
            request.Get_PeerView(), userId);

        if (peerId <= 0)
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400, "PEER_ID_INVALID"u8);
        }

        _ = peerType;
         
        return BoolTrue.Builder().Build();
    }
}
