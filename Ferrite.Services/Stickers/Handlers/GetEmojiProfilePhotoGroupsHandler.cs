// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class GetEmojiProfilePhotoGroupsHandler : StickerHandlerBase
{
    public GetEmojiProfilePhotoGroupsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        StickerStore store) : base(unitOfWork, authorizationRepository, store) { }

    [TLFunction(Constructors.baseLayer_GetEmojiProfilePhotoGroups)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        int hash = ((GetEmojiProfilePhotoGroups)q).Hash;
        return await GetUserIdAsync(authKeyId) is not null
            ? DefaultEmojiCatalog.GetGroups(EmojiGroupKind.ProfilePhoto, hash)
            : AuthError();
    }
}
