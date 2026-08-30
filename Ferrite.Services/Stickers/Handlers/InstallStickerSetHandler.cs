// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class InstallStickerSetHandler : StickerHandlerBase
{
    private readonly StickerCollectionStore _collections;

    public InstallStickerSetHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerCollectionStore store)
        : base(unitOfWork, authorizationRepository)
    {
        _collections = store;
    }

    [TLFunction(Constructors.baseLayer_InstallStickerSet)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (InstallStickerSet)q;
        var input = StickerInput.ReadInputSet(request.Get_StickersetView());
        bool archived = request.Archived;
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await _collections.InstallAsync(userId.Value, authKeyId, input.Id,
                input.AccessHash, input.ShortName, archived)
            : AuthError();
    }
}
