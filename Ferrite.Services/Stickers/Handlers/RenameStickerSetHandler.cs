// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.stickers;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class RenameStickerSetHandler : StickerHandlerBase
{
    private readonly StickerSetEditor _editor;

    public RenameStickerSetHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository, StickerSetEditor store)
        : base(unitOfWork, authorizationRepository)
    {
        _editor = store;
    }

    [TLFunction(Constructors.baseLayer_RenameStickerSet)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (RenameStickerSet)q;
        var set = StickerInput.ReadInputSet(request.Get_StickersetView());
        string title = Encoding.UTF8.GetString(request.Title);
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await _editor.RenameAsync(userId.Value, set.Id, set.AccessHash,
                set.ShortName, title) : AuthError();
    }
}
