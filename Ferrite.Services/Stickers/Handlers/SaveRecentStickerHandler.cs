// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class SaveRecentStickerHandler : StickerHandlerBase
{
    private readonly StickerCollectionStore _collections;

    public SaveRecentStickerHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerCollectionStore store)
        : base(unitOfWork, authorizationRepository)
    {
        _collections = store;
    }

    [TLFunction(Constructors.baseLayer_SaveRecentSticker)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (SaveRecentSticker)q;
        bool attached = request.Attached;
        bool unsave = request.Unsave;
        var input = StickerInput.ReadInputDocument(request.Get_IdView());
        if (!input.Id.HasValue || !input.AccessHash.HasValue)
            return Invalid("STICKER_ID_INVALID");
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await _collections.SaveCollectionDocumentAsync(userId.Value, authKeyId,
                input.Id.Value, input.AccessHash.Value, attached
                    ? StickerCollection.AttachedRecent
                    : StickerCollection.Recent, unsave)
            : AuthError();
    }
}
