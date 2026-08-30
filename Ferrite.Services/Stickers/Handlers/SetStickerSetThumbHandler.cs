// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.stickers;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class SetStickerSetThumbHandler : StickerHandlerBase
{
    private readonly StickerSetEditor _editor;

    public SetStickerSetThumbHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerSetEditor store)
        : base(unitOfWork, authorizationRepository)
    {
        _editor = store;
    }

    [TLFunction(Constructors.baseLayer_SetStickerSetThumb)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (SetStickerSetThumb)q;
        var set = StickerInput.ReadInputSet(request.Get_StickersetView());
        bool hasThumb = request.Flags[0];
        bool hasDocumentId = request.Flags[1];
        if (hasThumb == hasDocumentId) return Invalid("STICKER_ID_INVALID");
        long thumbId;
        long? thumbAccessHash;
        if (hasThumb)
        {
            var thumb = StickerInput.ReadInputDocument(request.Get_ThumbView());
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
            ? await _editor.SetThumbAsync(userId.Value, set.Id, set.AccessHash,
                set.ShortName, thumbId, thumbAccessHash) : AuthError();
    }
}
