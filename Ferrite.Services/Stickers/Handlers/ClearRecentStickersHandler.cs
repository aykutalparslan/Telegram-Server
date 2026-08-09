// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class ClearRecentStickersHandler : StickerHandlerBase
{
    public ClearRecentStickersHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, StickerStore store)
        : base(unitOfWork, authorizationRepository, store) { }

    [TLFunction(Constructors.baseLayer_ClearRecentStickers)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        bool attached = ((ClearRecentStickers)q).Attached;
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await Store.ClearRecentAsync(userId.Value, authKeyId, attached)
            : AuthError();
    }
}
