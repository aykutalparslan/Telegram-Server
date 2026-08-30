// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class GetStickersHandler : StickerHandlerBase
{
    private readonly StickerSearchIndex _search;

    public GetStickersHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerSearchIndex store)
        : base(unitOfWork, authorizationRepository)
    {
        _search = store;
    }

    [TLFunction(Constructors.baseLayer_GetStickers)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetStickers)q;
        string emoticon = Encoding.UTF8.GetString(request.Emoticon);
        long hash = request.Hash;
        return await GetUserIdAsync(authKeyId) is not null
            ? await _search.GetStickersAsync(emoticon, hash) : AuthError();
    }
}
