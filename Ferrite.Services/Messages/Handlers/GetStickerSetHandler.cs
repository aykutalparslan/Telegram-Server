// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetStickerSetHandler
{
    private readonly StickerSetCatalog _catalog;

    public GetStickerSetHandler(StickerSetCatalog catalog)
    {
        _catalog = catalog;
    }

    [TLFunction(Constructors.baseLayer_GetStickerSet)]
    public async ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        (long? setId, long? accessHash, string? shortName) =
            StickerInput.ReadInputSet(
            ((GetStickerSet)q).Get_StickersetView());
        TLBytes? result = await _catalog.GetFullSetAsync(setId, accessHash,
            shortName);
        return result ?? RpcErrorGenerator.GenerateError(400,
            "STICKERSET_INVALID"u8);
    }
}
