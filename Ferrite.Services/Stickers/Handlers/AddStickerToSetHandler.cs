// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.stickers;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class AddStickerToSetHandler : StickerHandlerBase
{
    private readonly StickerSetEditor _editor;

    public AddStickerToSetHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerSetEditor store)
        : base(unitOfWork, authorizationRepository)
    {
        _editor = store;
    }

    [TLFunction(Constructors.baseLayer_AddStickerToSet)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (AddStickerToSet)q;
        var set = StickerInput.ReadInputSet(request.Get_StickersetView());
        StickerItemInput? item = StickerInput.ReadItem(
            request.Get_StickerView());
        if (!item.HasValue) return Invalid("STICKER_INVALID");
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await _editor.AddAsync(userId.Value, set.Id, set.AccessHash,
                set.ShortName, item.Value) : AuthError();
    }
}
