// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class ClearAllDraftsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly DraftStore _drafts;

    public ClearAllDraftsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, DraftStore drafts)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _drafts = drafts;
    }

    [TLFunction(Constructors.baseLayer_ClearAllDrafts)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
    {
        using var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400,
                "AUTH_KEY_INVALID"u8);
        }

        bool cleared = await _drafts.ClearAllAsync(authKeyId,
            auth.Value.AsAuthInfo().UserId);
        // Code 500 is the pinned client's retry-storm branch; report the failure
        // with a code it surfaces instead of resending.
        return cleared
            ? BoolTrue.Builder().Build()
            : (TLBool)RpcErrorGenerator.GenerateError(400,
                "INTERNAL_SERVER_ERROR"u8);
    }
}
