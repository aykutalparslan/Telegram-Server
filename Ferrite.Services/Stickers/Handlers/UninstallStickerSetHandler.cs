// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class UninstallStickerSetHandler : StickerHandlerBase
{
    public UninstallStickerSetHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, StickerStore store)
        : base(unitOfWork, authorizationRepository, store) { }

    [TLFunction(Constructors.baseLayer_UninstallStickerSet)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var input = StickerStore.ReadInputSet(
            ((UninstallStickerSet)q).Get_StickersetView());
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await Store.UninstallAsync(userId.Value, authKeyId, input.Id,
                input.AccessHash, input.ShortName)
            : AuthError();
    }
}
