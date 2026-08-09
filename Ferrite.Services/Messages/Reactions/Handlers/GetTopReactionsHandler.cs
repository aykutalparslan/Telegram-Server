// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.Reactions;

public sealed class GetTopReactionsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;

    public GetTopReactionsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
    }

    [TLFunction(Constructors.baseLayer_GetTopReactions)]
    public async Task<TLReactions> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (TLReactions)RpcErrorGenerator.GenerateError(400,
                "AUTH_KEY_INVALID"u8);
        }

        var request = (GetTopReactions)q;
        int limit = request.Limit;
        long clientHash = request.Hash;
        var reactions = DefaultReactions.DefaultReactionBytes.ToList();
        if (limit > 0 && limit < reactions.Count)
        {
            reactions = reactions.Take(limit).ToList();
        }

        return ReactionStore.BuildReactionsResultValue(reactions, clientHash);
    }
}
