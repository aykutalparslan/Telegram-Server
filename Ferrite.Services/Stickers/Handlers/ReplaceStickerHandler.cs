// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.stickers;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class ReplaceStickerHandler : StickerHandlerBase
{
    public ReplaceStickerHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, StickerStore store)
        : base(unitOfWork, authorizationRepository, store) { }

    [TLFunction(Constructors.baseLayer_ReplaceSticker)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (ReplaceSticker)q;
        var old = StickerStore.ReadInputDocument(request.Get_StickerView());
        StickerStore.StickerItemInput? replacement = StickerStore.ReadItem(
            request.Get_NewStickerView());
        if (!old.Id.HasValue || !old.AccessHash.HasValue ||
            !replacement.HasValue) return Invalid("STICKER_ID_INVALID");
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await Store.ReplaceAsync(userId.Value, old.Id.Value,
                old.AccessHash.Value, replacement.Value) : AuthError();
    }
}
