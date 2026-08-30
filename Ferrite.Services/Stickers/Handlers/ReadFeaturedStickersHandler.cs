// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class ReadFeaturedStickersHandler : StickerHandlerBase
{
    private readonly StickerCollectionStore _collections;

    public ReadFeaturedStickersHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerCollectionStore store)
        : base(unitOfWork, authorizationRepository)
    {
        _collections = store;
    }

    [TLFunction(Constructors.baseLayer_ReadFeaturedStickers)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long[] ids = ((ReadFeaturedStickers)q).Id.ToArray();
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await _collections.ReadFeaturedAsync(userId.Value, authKeyId, ids)
            : AuthError();
    }
}
