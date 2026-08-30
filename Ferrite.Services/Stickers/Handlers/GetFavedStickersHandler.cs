// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class GetFavedStickersHandler : StickerHandlerBase
{
    private readonly StickerCollectionStore _collections;

    public GetFavedStickersHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerCollectionStore store)
        : base(unitOfWork, authorizationRepository)
    {
        _collections = store;
    }

    [TLFunction(Constructors.baseLayer_GetFavedStickers)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long hash = ((GetFavedStickers)q).Hash;
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await _collections.GetFavedAsync(userId.Value, hash) : AuthError();
    }
}
