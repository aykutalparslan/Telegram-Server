// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.contacts;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.ContactMethods;

public sealed class GetTopPeersHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly ITopPeersRepository _topPeersRepository;

    private readonly IUnitOfWork _unitOfWork;

    public GetTopPeersHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, ITopPeersRepository topPeersRepository) {
        _authorizationRepository = authorizationRepository;
        _topPeersRepository = topPeersRepository;
        _unitOfWork = unitOfWork;
    }

    [TLFunction(Constructors.baseLayer_GetTopPeers)]
    public async ValueTask<TLTopPeers> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return (TLTopPeers)RpcErrorGenerator.GenerateError(401, "AUTH_KEY_INVALID"u8);
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        bool disabled;
        using (TLTopPeersState? state = await _topPeersRepository
                   .GetStateAsync(userId))
        {
            disabled = state != null && state.Value.AsTopPeersState().Flags[0];
        }

        if (disabled)
        {
            return TopPeersDisabled.Builder().Build();
        }

        return TopPeers.Builder()
            .Categories(new Vector())
            .Chats(new Vector())
            .Users(new Vector())
            .Build();
    }
}
