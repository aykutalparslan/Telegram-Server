// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class GetSavedGifsHandler : StickerHandlerBase
{
    public GetSavedGifsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, StickerStore store)
        : base(unitOfWork, authorizationRepository, store) { }

    [TLFunction(Constructors.baseLayer_GetSavedGifs)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long hash = ((GetSavedGifs)q).Hash;
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await Store.GetSavedGifsAsync(userId.Value, hash) : AuthError();
    }
}
