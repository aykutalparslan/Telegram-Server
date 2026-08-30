// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.stickers;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class ChangeStickerHandler : StickerHandlerBase
{
    private readonly StickerSetEditor _editor;

    public ChangeStickerHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerSetEditor store)
        : base(unitOfWork, authorizationRepository)
    {
        _editor = store;
    }

    [TLFunction(Constructors.baseLayer_ChangeSticker)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (ChangeSticker)q;
        var sticker = StickerInput.ReadInputDocument(request.Get_StickerView());
        string? emoji = request.Flags[0]
            ? Encoding.UTF8.GetString(request.Emoji) : null;
        byte[]? maskCoords = request.Flags[1]
            ? request.MaskCoords.ToArray() : null;
        string? keywords = request.Flags[2]
            ? Encoding.UTF8.GetString(request.Keywords) : null;
        if (!sticker.Id.HasValue || !sticker.AccessHash.HasValue)
            return Invalid("STICKER_ID_INVALID");
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await _editor.ChangeAsync(userId.Value, sticker.Id.Value,
                sticker.AccessHash.Value, emoji, maskCoords, keywords)
            : AuthError();
    }
}
