// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.stickers;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class SuggestShortNameHandler : StickerHandlerBase
{
    private readonly StickerSetEditor _editor;

    public SuggestShortNameHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerSetEditor store)
        : base(unitOfWork, authorizationRepository)
    {
        _editor = store;
    }

    [TLFunction(Constructors.baseLayer_SuggestShortName)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        string title = Encoding.UTF8.GetString(((SuggestShortName)q).Title);
        return await GetUserIdAsync(authKeyId) is not null
            ? await _editor.SuggestShortNameAsync(title) : AuthError();
    }
}
