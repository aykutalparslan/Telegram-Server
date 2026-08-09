// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Services.Handlers.AccountMethods;
using Ferrite.TL;
using Ferrite.TL.baseLayer.users;

namespace Ferrite.Services.Handlers.UserMethods;

public sealed class GetSavedMusicHandler : AccountAudioHandlerBase
{
    public GetSavedMusicHandler(AccountAudioStore store, ProfileStore profiles)
        : base(store, profiles) { }

    [TLFunction(Constructors.baseLayer_GetSavedMusic)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new GetSavedMusic(q.AsSpan());
        if (!TryReadUser(request.Get_IdView(), out var input)) return UserError();
        return await Store.GetMusicAsync(userId.Value, input, request.Offset,
            request.Limit, request.Hash);
    }
}
