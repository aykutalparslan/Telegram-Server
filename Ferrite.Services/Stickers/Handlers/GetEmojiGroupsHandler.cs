// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class GetEmojiGroupsHandler : StickerHandlerBase
{
    public GetEmojiGroupsHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository)
        : base(unitOfWork, authorizationRepository) { }

    [TLFunction(Constructors.baseLayer_GetEmojiGroups)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        int hash = ((GetEmojiGroups)q).Hash;
        return await GetUserIdAsync(authKeyId) is not null
            ? DefaultEmojiCatalog.GetGroups(EmojiGroupKind.General, hash)
            : AuthError();
    }
}
