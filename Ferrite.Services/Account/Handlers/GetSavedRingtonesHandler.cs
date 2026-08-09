// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class GetSavedRingtonesHandler : AccountAudioHandlerBase
{
    public GetSavedRingtonesHandler(AccountAudioStore store,
        ProfileStore profiles) : base(store, profiles) { }

    [TLFunction(Constructors.baseLayer_GetSavedRingtones)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        return await Store.GetRingtonesAsync(userId.Value,
            new GetSavedRingtones(q.AsSpan()).Hash);
    }
}
