// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.Reactions;

public sealed class GetRecentReactionsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ReactionStore _reactions;

    public GetRecentReactionsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, ReactionStore reactions)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _reactions = reactions;
    }

    [TLFunction(Constructors.baseLayer_GetRecentReactions)]
    public async Task<TLReactions> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (TLReactions)RpcErrorGenerator.GenerateError(400,
                "AUTH_KEY_INVALID"u8);
        }

        long userId = auth.Value.AsAuthInfo().UserId;
        var request = (GetRecentReactions)q;
        int limit = request.Limit;
        long clientHash = request.Hash;
        var (_, recent) = await _reactions.ReadReactionSettingsAsync(userId);
        if (limit > 0 && limit < recent.Count)
        {
            recent = recent.Take(limit).ToList();
        }

        return ReactionStore.BuildReactionsResultValue(recent, clientHash);
    }
}
