// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class ClearRecentStickersHandler : StickerHandlerBase
{
    private readonly StickerCollectionStore _collections;

    public ClearRecentStickersHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerCollectionStore store)
        : base(unitOfWork, authorizationRepository)
    {
        _collections = store;
    }

    [TLFunction(Constructors.baseLayer_ClearRecentStickers)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        bool attached = ((ClearRecentStickers)q).Attached;
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await _collections.ClearRecentAsync(userId.Value, authKeyId, attached)
            : AuthError();
    }
}
