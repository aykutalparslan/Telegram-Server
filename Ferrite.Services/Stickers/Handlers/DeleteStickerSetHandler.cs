// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.stickers;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class DeleteStickerSetHandler : StickerHandlerBase
{
    public DeleteStickerSetHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, StickerStore store)
        : base(unitOfWork, authorizationRepository, store) { }

    [TLFunction(Constructors.baseLayer_DeleteStickerSet)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var set = StickerStore.ReadInputSet(
            ((DeleteStickerSet)q).Get_StickersetView());
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await Store.DeleteAsync(userId.Value, set.Id, set.AccessHash,
                set.ShortName) : AuthError();
    }
}
