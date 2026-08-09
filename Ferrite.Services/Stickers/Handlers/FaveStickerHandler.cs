// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class FaveStickerHandler : StickerHandlerBase
{
    public FaveStickerHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, StickerStore store)
        : base(unitOfWork, authorizationRepository, store) { }

    [TLFunction(Constructors.baseLayer_FaveSticker)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (FaveSticker)q;
        var input = StickerStore.ReadInputDocument(request.Get_IdView());
        bool unfave = request.Unfave;
        if (!input.Id.HasValue || !input.AccessHash.HasValue)
            return Invalid("STICKER_ID_INVALID");
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await Store.SaveCollectionDocumentAsync(userId.Value, authKeyId,
                input.Id.Value, input.AccessHash.Value,
                StickerStore.AccountCollection.Faved, unfave)
            : AuthError();
    }
}
