// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class ReorderStickerSetsHandler : StickerHandlerBase
{
    private readonly StickerCollectionStore _collections;

    public ReorderStickerSetsHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerCollectionStore store)
        : base(unitOfWork, authorizationRepository)
    {
        _collections = store;
    }

    [TLFunction(Constructors.baseLayer_ReorderStickerSets)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (ReorderStickerSets)q;
        if (request.Masks && request.Emojis) return Invalid("STICKERSET_INVALID");
        StickerSetKind kind = request.Emojis ? StickerSetKind.Emoji
            : request.Masks ? StickerSetKind.Mask : StickerSetKind.Regular;
        long[] order = request.Order.ToArray();
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await _collections.ReorderAsync(userId.Value, authKeyId, kind, order)
            : AuthError();
    }
}
