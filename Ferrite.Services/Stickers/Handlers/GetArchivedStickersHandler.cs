// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class GetArchivedStickersHandler : StickerHandlerBase
{
    public GetArchivedStickersHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, StickerStore store)
        : base(unitOfWork, authorizationRepository, store) { }

    [TLFunction(Constructors.baseLayer_GetArchivedStickers)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetArchivedStickers)q;
        if (request.Masks && request.Emojis)
        {
            return RpcErrorGenerator.GenerateError(400,
                "STICKERSET_INVALID"u8);
        }
        if (request.Limit is <= 0 or > 200)
        {
            return LimitError();
        }
        StickerSetKind kind = request.Emojis ? StickerSetKind.Emoji
            : request.Masks ? StickerSetKind.Mask : StickerSetKind.Regular;
        long offsetId = request.OffsetId;
        int limit = request.Limit;
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await Store.GetArchivedAsync(userId.Value, kind, offsetId, limit)
            : AuthError();
    }
}
