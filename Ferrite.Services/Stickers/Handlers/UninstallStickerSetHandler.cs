// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class UninstallStickerSetHandler : StickerHandlerBase
{
    private readonly StickerCollectionStore _collections;

    public UninstallStickerSetHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerCollectionStore store)
        : base(unitOfWork, authorizationRepository)
    {
        _collections = store;
    }

    [TLFunction(Constructors.baseLayer_UninstallStickerSet)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var input = StickerInput.ReadInputSet(
            ((UninstallStickerSet)q).Get_StickersetView());
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await _collections.UninstallAsync(userId.Value, authKeyId, input.Id,
                input.AccessHash, input.ShortName)
            : AuthError();
    }
}
