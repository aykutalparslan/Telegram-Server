// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class GetAttachedStickersHandler : StickerHandlerBase
{
    public GetAttachedStickersHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, StickerStore store)
        : base(unitOfWork, authorizationRepository, store) { }

    [TLFunction(Constructors.baseLayer_GetAttachedStickers)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? documentId = null;
        long? accessHash = null;
        InputStickeredMediaView media = ((GetAttachedStickers)q).Get_MediaView();
        if (media.Is(out InputStickeredMediaDocument document))
        {
            (documentId, accessHash) = StickerStore.ReadInputDocument(
                document.Get_IdView());
            if (!documentId.HasValue || !accessHash.HasValue)
                return Invalid("STICKER_ID_INVALID");
        }
        else if (!media.Is(out InputStickeredMediaPhoto _))
        {
            return Invalid("STICKER_ID_INVALID");
        }
        return await GetUserIdAsync(authKeyId) is not null
            ? await Store.GetAttachedAsync(documentId, accessHash) : AuthError();
    }
}
