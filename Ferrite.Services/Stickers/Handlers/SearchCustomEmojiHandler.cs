// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class SearchCustomEmojiHandler : StickerHandlerBase
{
    public SearchCustomEmojiHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, StickerStore store)
        : base(unitOfWork, authorizationRepository, store) { }

    [TLFunction(Constructors.baseLayer_SearchCustomEmoji)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (SearchCustomEmoji)q;
        string emoticon = Encoding.UTF8.GetString(request.Emoticon);
        long hash = request.Hash;
        return await GetUserIdAsync(authKeyId) is not null
            ? await Store.SearchCustomEmojiAsync(emoticon, hash) : AuthError();
    }
}
