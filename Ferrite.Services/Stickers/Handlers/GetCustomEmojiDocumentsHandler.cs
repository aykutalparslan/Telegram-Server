// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class GetCustomEmojiDocumentsHandler : StickerHandlerBase
{
    private readonly StickerSearchIndex _search;

    public GetCustomEmojiDocumentsHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerSearchIndex store)
        : base(unitOfWork, authorizationRepository)
    {
        _search = store;
    }

    [TLFunction(Constructors.baseLayer_GetCustomEmojiDocuments)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long[] ids = ((GetCustomEmojiDocuments)q).DocumentId.ToArray();
        return await GetUserIdAsync(authKeyId) is not null
            ? await _search.GetCustomEmojiDocumentsAsync(ids) : AuthError();
    }
}
