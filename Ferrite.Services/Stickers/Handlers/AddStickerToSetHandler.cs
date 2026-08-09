// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.stickers;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class AddStickerToSetHandler : StickerHandlerBase
{
    public AddStickerToSetHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, StickerStore store)
        : base(unitOfWork, authorizationRepository, store) { }

    [TLFunction(Constructors.baseLayer_AddStickerToSet)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (AddStickerToSet)q;
        var set = StickerStore.ReadInputSet(request.Get_StickersetView());
        StickerStore.StickerItemInput? item = StickerStore.ReadItem(
            request.Get_StickerView());
        if (!item.HasValue) return Invalid("STICKER_INVALID");
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await Store.AddAsync(userId.Value, set.Id, set.AccessHash,
                set.ShortName, item.Value) : AuthError();
    }
}
