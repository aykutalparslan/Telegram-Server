// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.stickers;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class CheckShortNameHandler : StickerHandlerBase
{
    public CheckShortNameHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, StickerStore store)
        : base(unitOfWork, authorizationRepository, store) { }

    [TLFunction(Constructors.baseLayer_CheckShortName)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        string shortName = Encoding.UTF8.GetString(((CheckShortName)q).ShortName);
        return await GetUserIdAsync(authKeyId) is not null
            ? await Store.CheckShortNameAsync(shortName) : AuthError();
    }
}
