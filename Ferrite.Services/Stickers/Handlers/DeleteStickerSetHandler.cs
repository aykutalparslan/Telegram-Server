// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.stickers;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class DeleteStickerSetHandler : StickerHandlerBase
{
    private readonly StickerSetEditor _editor;

    public DeleteStickerSetHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerSetEditor store)
        : base(unitOfWork, authorizationRepository)
    {
        _editor = store;
    }

    [TLFunction(Constructors.baseLayer_DeleteStickerSet)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var set = StickerInput.ReadInputSet(
            ((DeleteStickerSet)q).Get_StickersetView());
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await _editor.DeleteAsync(userId.Value, set.Id, set.AccessHash,
                set.ShortName) : AuthError();
    }
}
