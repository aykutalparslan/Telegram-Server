// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.StickerMethods;

public abstract class StickerHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;

    protected readonly IUnitOfWork UnitOfWork;
    protected readonly StickerStore Store;

    protected StickerHandlerBase(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, StickerStore store)
    {
        _authorizationRepository = authorizationRepository;

        UnitOfWork = unitOfWork;
        Store = store;
    }

    protected async ValueTask<long?> GetUserIdAsync(long authKeyId)
    {
        using TLAuthInfo? auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        return auth is not null && auth.Value.AsAuthInfo().LoggedIn
            ? auth.Value.AsAuthInfo().UserId : null;
    }

    protected static TLBytes AuthError() =>
        RpcErrorGenerator.GenerateError(401, "AUTH_KEY_INVALID"u8);

    protected static TLBytes LimitError() =>
        RpcErrorGenerator.GenerateError(400, "LIMIT_INVALID"u8);

    protected static TLBytes Invalid(string message) =>
        RpcErrorGenerator.GenerateError(400,
            System.Text.Encoding.UTF8.GetBytes(message));

    protected static bool IsSelf(InputUserView input, long userId)
    {
        if (input.Is(out InputUserSelf _)) return true;
        return input.Is(out InputUser user) && user.UserId == userId;
    }
}
