// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.Channels;

/// <summary>
/// Reports what a public-post search will cost. Pinned TDLib issues this ahead of
/// `td_api::searchPublicPosts` through `CheckSearchPostsFloodQuery`
/// (`MessageQueryManager.cpp:521`) and shows the answer as the user's remaining
/// free allowance.
///
/// Ferrite runs no paid search, so the answer does not depend on the query or on
/// how many searches the caller has already run: see
/// <see cref="PublicPostSearchPolicy"/>.
/// </summary>
public sealed class CheckSearchPostsFloodHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;

    public CheckSearchPostsFloodHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
    }

    [TLFunction(Constructors.baseLayer_CheckSearchPostsFlood)]
    public async Task<TLSearchPostsFlood> Handle(long authKeyId, TLBytes q)
    {
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return (TLSearchPostsFlood)RpcErrorGenerator.GenerateError(401,
                    "AUTH_KEY_INVALID"u8);
            }
        }

        return PublicPostSearchPolicy.BuildFlood();
    }
}
