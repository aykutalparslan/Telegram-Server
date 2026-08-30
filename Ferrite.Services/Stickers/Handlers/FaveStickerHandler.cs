// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class FaveStickerHandler : StickerHandlerBase
{
    private readonly StickerCollectionStore _collections;

    public FaveStickerHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerCollectionStore store)
        : base(unitOfWork, authorizationRepository)
    {
        _collections = store;
    }

    [TLFunction(Constructors.baseLayer_FaveSticker)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (FaveSticker)q;
        var input = StickerInput.ReadInputDocument(request.Get_IdView());
        bool unfave = request.Unfave;
        if (!input.Id.HasValue || !input.AccessHash.HasValue)
            return Invalid("STICKER_ID_INVALID");
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await _collections.SaveCollectionDocumentAsync(userId.Value, authKeyId,
                input.Id.Value, input.AccessHash.Value,
                StickerCollection.Faved, unfave)
            : AuthError();
    }
}
