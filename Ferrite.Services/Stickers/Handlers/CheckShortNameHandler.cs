// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.stickers;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class CheckShortNameHandler : StickerHandlerBase
{
    private readonly StickerSetEditor _editor;

    public CheckShortNameHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerSetEditor store)
        : base(unitOfWork, authorizationRepository)
    {
        _editor = store;
    }

    [TLFunction(Constructors.baseLayer_CheckShortName)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        string shortName = Encoding.UTF8.GetString(((CheckShortName)q).ShortName);
        return await GetUserIdAsync(authKeyId) is not null
            ? await _editor.CheckShortNameAsync(shortName) : AuthError();
    }
}
