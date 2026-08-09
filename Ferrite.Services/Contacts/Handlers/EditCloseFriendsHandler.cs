// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.contacts;

namespace Ferrite.Services.Handlers.ContactMethods;

public sealed class EditCloseFriendsHandler
{
    private readonly ProfileStore _profiles;

    public EditCloseFriendsHandler(ProfileStore profiles) => _profiles = profiles;

    [TLFunction(Constructors.baseLayer_EditCloseFriends)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await _profiles.GetUserIdAsync(authKeyId);
        if (!userId.HasValue)
            return RpcErrorGenerator.GenerateError(401, "AUTH_KEY_INVALID"u8);
        long[] ids = new EditCloseFriends(q.AsSpan()).Id.ToArray();
        return await _profiles.ReplaceCloseFriendsAsync(userId.Value, ids);
    }
}
