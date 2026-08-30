// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.contacts;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.ContactMethods;

public sealed class ToggleTopPeersHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly ITopPeersRepository _topPeersRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ToggleTopPeersHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, ITopPeersRepository topPeersRepository, TimeProvider timeProvider)
    {
        _authorizationRepository = authorizationRepository;
        _topPeersRepository = topPeersRepository;

        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    [TLFunction(Constructors.baseLayer_ToggleTopPeers)]
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

        var request = (ToggleTopPeers)q;
        bool enabled = request.Enabled;

        int now = checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());
        var builder = TopPeersState.Builder().UserId(userId).Date(now);
        if (!enabled)
        {
            builder = builder.Disabled(true);
        }

        using TLTopPeersState state = builder.Build();
        if (!_topPeersRepository.PutState(state) ||
            !await _unitOfWork.SaveAsync())
        {
            return (TLBool)RpcErrorGenerator.GenerateError(500, "STORAGE_FAILED"u8);
        }

         
        return BoolTrue.Builder().Build();
    }
}
