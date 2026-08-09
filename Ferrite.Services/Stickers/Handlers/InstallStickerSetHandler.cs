// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class InstallStickerSetHandler : StickerHandlerBase
{
    public InstallStickerSetHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, StickerStore store)
        : base(unitOfWork, authorizationRepository, store) { }

    [TLFunction(Constructors.baseLayer_InstallStickerSet)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (InstallStickerSet)q;
        var input = StickerStore.ReadInputSet(request.Get_StickersetView());
        bool archived = request.Archived;
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await Store.InstallAsync(userId.Value, authKeyId, input.Id,
                input.AccessHash, input.ShortName, archived)
            : AuthError();
    }
}
