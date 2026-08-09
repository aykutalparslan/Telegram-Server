// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class GetMyStickersHandler : StickerHandlerBase
{
    public GetMyStickersHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, StickerStore store)
        : base(unitOfWork, authorizationRepository, store) { }

    [TLFunction(Constructors.baseLayer_GetMyStickers)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetMyStickers)q;
        if (request.Limit is <= 0 or > 200)
        {
            return LimitError();
        }
        long offsetId = request.OffsetId;
        int limit = request.Limit;
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await Store.GetOwnedAsync(userId.Value, offsetId, limit)
            : AuthError();
    }
}
