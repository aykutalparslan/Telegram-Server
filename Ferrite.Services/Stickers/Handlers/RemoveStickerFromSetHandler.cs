// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.stickers;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class RemoveStickerFromSetHandler : StickerHandlerBase
{
    public RemoveStickerFromSetHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, StickerStore store)
        : base(unitOfWork, authorizationRepository, store) { }

    [TLFunction(Constructors.baseLayer_RemoveStickerFromSet)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var sticker = StickerStore.ReadInputDocument(
            ((RemoveStickerFromSet)q).Get_StickerView());
        if (!sticker.Id.HasValue || !sticker.AccessHash.HasValue)
            return Invalid("STICKER_ID_INVALID");
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await Store.RemoveAsync(userId.Value, sticker.Id.Value,
                sticker.AccessHash.Value) : AuthError();
    }
}
