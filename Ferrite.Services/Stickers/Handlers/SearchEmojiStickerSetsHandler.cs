// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class SearchEmojiStickerSetsHandler : StickerHandlerBase
{
    public SearchEmojiStickerSetsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        StickerStore store) : base(unitOfWork, authorizationRepository, store) { }

    [TLFunction(Constructors.baseLayer_SearchEmojiStickerSets)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (SearchEmojiStickerSets)q;
        byte[] query = request.Q.ToArray();
        long hash = request.Hash;
        return await GetUserIdAsync(authKeyId) is not null
            ? await Store.SearchSetsAsync(StickerSetKind.Emoji, query, hash)
            : AuthError();
    }
}
