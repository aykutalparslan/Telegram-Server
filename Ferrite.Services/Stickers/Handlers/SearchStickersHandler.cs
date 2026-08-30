// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class SearchStickersHandler : StickerHandlerBase
{
    private readonly StickerSearchIndex _search;

    public SearchStickersHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerSearchIndex store)
        : base(unitOfWork, authorizationRepository)
    {
        _search = store;
    }

    [TLFunction(Constructors.baseLayer_SearchStickers)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (SearchStickers)q;
        bool emojis = request.Emojis;
        string query = Encoding.UTF8.GetString(request.Q);
        string emoticon = Encoding.UTF8.GetString(request.Emoticon);
        int offset = request.Offset;
        int limit = request.Limit;
        long hash = request.Hash;
        return await GetUserIdAsync(authKeyId) is not null
            ? await _search.SearchDocumentsAsync(emojis, query, emoticon, offset,
                limit, hash)
            : AuthError();
    }
}
