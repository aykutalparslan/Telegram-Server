// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.stickers;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class SetStickerSetThumbHandler : StickerHandlerBase
{
    public SetStickerSetThumbHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, StickerStore store)
        : base(unitOfWork, authorizationRepository, store) { }

    [TLFunction(Constructors.baseLayer_SetStickerSetThumb)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (SetStickerSetThumb)q;
        var set = StickerStore.ReadInputSet(request.Get_StickersetView());
        bool hasThumb = request.Flags[0];
        bool hasDocumentId = request.Flags[1];
        if (hasThumb == hasDocumentId) return Invalid("STICKER_ID_INVALID");
        long thumbId;
        long? thumbAccessHash;
        if (hasThumb)
        {
            var thumb = StickerStore.ReadInputDocument(request.Get_ThumbView());
            if (!thumb.Id.HasValue || !thumb.AccessHash.HasValue)
                return Invalid("STICKER_ID_INVALID");
            thumbId = thumb.Id.Value;
            thumbAccessHash = thumb.AccessHash.Value;
        }
        else
        {
            thumbId = request.ThumbDocumentId;
            thumbAccessHash = null;
            if (thumbId == 0) return Invalid("STICKER_ID_INVALID");
        }
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await Store.SetThumbAsync(userId.Value, set.Id, set.AccessHash,
                set.ShortName, thumbId, thumbAccessHash) : AuthError();
    }
}
