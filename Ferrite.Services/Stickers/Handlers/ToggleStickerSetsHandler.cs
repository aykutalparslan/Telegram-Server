// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class ToggleStickerSetsHandler : StickerHandlerBase
{
    private readonly StickerSetLookup _lookup;
    private readonly StickerCollectionStore _collections;

    public ToggleStickerSetsHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository,
        StickerSetLookup lookup, StickerCollectionStore store)
        : base(unitOfWork, authorizationRepository)
    {
        _lookup = lookup;
        _collections = store;
    }

    [TLFunction(Constructors.baseLayer_ToggleStickerSets)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (ToggleStickerSets)q;
        bool uninstall = request.Uninstall;
        bool archive = request.Archive;
        bool unarchive = request.Unarchive;
        Vector source = request.Stickersets;
        int count = source.Count;
        var inputs = new List<(long? Id, long? AccessHash, string? ShortName)>(count);
        for (int i = 0; i < count; i++)
        {
            inputs.Add(StickerInput.ReadInputSet(
                (InputStickerSetView)source.ReadTLObject()));
        }
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        long[]? ids = await _lookup.ResolveSetIdsAsync(inputs);
        return ids is null ? Invalid("STICKERSET_INVALID")
            : await _collections.ToggleSetsAsync(userId.Value, authKeyId, ids,
                uninstall, archive, unarchive);
    }
}
