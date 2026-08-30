// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class GetFeaturedEmojiStickersHandler : StickerHandlerBase
{
    public GetFeaturedEmojiStickersHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository)
        : base(unitOfWork, authorizationRepository) { }

    [TLFunction(Constructors.baseLayer_GetFeaturedEmojiStickers)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long hash = ((GetFeaturedEmojiStickers)q).Hash;
        return await GetUserIdAsync(authKeyId) is not null
            ? StickerSetCatalog.EmptyFeatured(hash) : AuthError();
    }
}
