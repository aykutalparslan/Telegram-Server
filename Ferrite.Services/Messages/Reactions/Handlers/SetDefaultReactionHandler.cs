// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Reactions;

public sealed class SetDefaultReactionHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ReactionStore _reactions;
    private readonly ILogger _log;

    public SetDefaultReactionHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, ReactionStore reactions,
        ILogger log)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _reactions = reactions;
        _log = log;
    }

    [TLFunction(Constructors.baseLayer_SetDefaultReaction)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return Error("AUTH_KEY_INVALID");
        }

        long userId = auth.Value.AsAuthInfo().UserId;
        byte[] reactionBytes = ((Ferrite.TL.baseLayer.messages.SetDefaultReaction)q)
            .Reaction.ToArray();
        var emoji = (ReactionEmoji)reactionBytes.AsSpan();
        if (emoji.Constructor != Constructors.baseLayer_ReactionEmoji ||
            !DefaultReactions.IsDefaultEmoji(emoji.Emoticon))
        {
            return Error("REACTION_INVALID");
        }

        var (_, recent) = await _reactions.ReadReactionSettingsAsync(userId);
        _reactions.PutReactionSettings(userId, reactionBytes, recent);
        await _unitOfWork.SaveAsync();
        _log.Debug($"💟 SetDefaultReaction user:{userId}");
        return BoolTrue.Builder().Build();
    }

    private static TLBool Error(string message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400,
            System.Text.Encoding.UTF8.GetBytes(message));
}
