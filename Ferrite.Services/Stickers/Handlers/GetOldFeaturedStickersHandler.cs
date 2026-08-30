// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class GetOldFeaturedStickersHandler : StickerHandlerBase
{
    public GetOldFeaturedStickersHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository)
        : base(unitOfWork, authorizationRepository) { }

    [TLFunction(Constructors.baseLayer_GetOldFeaturedStickers)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetOldFeaturedStickers)q;
        if (request.Limit is <= 0 or > 200)
        {
            return LimitError();
        }
        long hash = request.Hash;
        return await GetUserIdAsync(authKeyId) is not null
            ? StickerSetCatalog.EmptyFeatured(hash) : AuthError();
    }
}
