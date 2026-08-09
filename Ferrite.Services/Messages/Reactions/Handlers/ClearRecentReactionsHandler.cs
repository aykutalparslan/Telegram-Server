// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Reactions;

public sealed class ClearRecentReactionsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ReactionStore _reactions;
    private readonly UpdateFanout _fanout;
    private readonly ILogger _log;

    public ClearRecentReactionsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, ReactionStore reactions,
        UpdateFanout fanout, ILogger log)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _reactions = reactions;
        _fanout = fanout;
        _log = log;
    }

    [TLFunction(Constructors.baseLayer_ClearRecentReactions)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return Error("AUTH_KEY_INVALID");
        }

        long userId = auth.Value.AsAuthInfo().UserId;
        var (defaultReaction, _) = await _reactions.ReadReactionSettingsAsync(userId);
        _reactions.PutReactionSettings(userId, defaultReaction, new List<byte[]>());
        await _unitOfWork.SaveAsync();
        await _fanout.EnqueueRecentReactionsAsync(userId);
        _log.Debug($"💟 ClearRecentReactions user:{userId}");
        return BoolTrue.Builder().Build();
    }

    private static TLBool Error(string message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400,
            System.Text.Encoding.UTF8.GetBytes(message));
}
