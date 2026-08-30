// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.stickers;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class ChangeStickerPositionHandler : StickerHandlerBase
{
    private readonly StickerSetEditor _editor;

    public ChangeStickerPositionHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerSetEditor store)
        : base(unitOfWork, authorizationRepository)
    {
        _editor = store;
    }

    [TLFunction(Constructors.baseLayer_ChangeStickerPosition)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (ChangeStickerPosition)q;
        var sticker = StickerInput.ReadInputDocument(request.Get_StickerView());
        int position = request.Position;
        if (!sticker.Id.HasValue || !sticker.AccessHash.HasValue)
            return Invalid("STICKER_ID_INVALID");
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await _editor.MoveAsync(userId.Value, sticker.Id.Value,
                sticker.AccessHash.Value, position) : AuthError();
    }
}
