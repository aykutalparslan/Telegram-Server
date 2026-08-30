// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class GetAttachedStickersHandler : StickerHandlerBase
{
    private readonly StickerSearchIndex _search;

    public GetAttachedStickersHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerSearchIndex store)
        : base(unitOfWork, authorizationRepository)
    {
        _search = store;
    }

    [TLFunction(Constructors.baseLayer_GetAttachedStickers)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? documentId = null;
        long? accessHash = null;
        InputStickeredMediaView media = ((GetAttachedStickers)q).Get_MediaView();
        if (media.Is(out InputStickeredMediaDocument document))
        {
            (documentId, accessHash) = StickerInput.ReadInputDocument(
                document.Get_IdView());
            if (!documentId.HasValue || !accessHash.HasValue)
                return Invalid("STICKER_ID_INVALID");
        }
        else if (!media.Is(out InputStickeredMediaPhoto _))
        {
            return Invalid("STICKER_ID_INVALID");
        }
        return await GetUserIdAsync(authKeyId) is not null
            ? await _search.GetAttachedAsync(documentId, accessHash) : AuthError();
    }
}
