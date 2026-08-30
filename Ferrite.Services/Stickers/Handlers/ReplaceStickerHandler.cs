// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.stickers;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class ReplaceStickerHandler : StickerHandlerBase
{
    private readonly StickerSetEditor _editor;

    public ReplaceStickerHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerSetEditor store)
        : base(unitOfWork, authorizationRepository)
    {
        _editor = store;
    }

    [TLFunction(Constructors.baseLayer_ReplaceSticker)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (ReplaceSticker)q;
        var old = StickerInput.ReadInputDocument(request.Get_StickerView());
        StickerItemInput? replacement = StickerInput.ReadItem(
            request.Get_NewStickerView());
        if (!old.Id.HasValue || !old.AccessHash.HasValue ||
            !replacement.HasValue) return Invalid("STICKER_ID_INVALID");
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await _editor.ReplaceAsync(userId.Value, old.Id.Value,
                old.AccessHash.Value, replacement.Value) : AuthError();
    }
}
