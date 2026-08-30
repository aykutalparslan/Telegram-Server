// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class GetAllStickersHandler : StickerHandlerBase
{
    private readonly StickerSetCatalog _catalog;

    public GetAllStickersHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerSetCatalog store)
        : base(unitOfWork, authorizationRepository)
    {
        _catalog = store;
    }

    [TLFunction(Constructors.baseLayer_GetAllStickers)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long hash = ((GetAllStickers)q).Hash;
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await _catalog.GetInstalledAsync(userId.Value, StickerSetKind.Regular,
                hash)
            : AuthError();
    }
}
