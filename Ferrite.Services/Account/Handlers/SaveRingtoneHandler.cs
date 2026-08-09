// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class SaveRingtoneHandler : AccountAudioHandlerBase
{
    public SaveRingtoneHandler(AccountAudioStore store, ProfileStore profiles)
        : base(store, profiles) { }

    [TLFunction(Constructors.baseLayer_SaveRingtone)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new SaveRingtone(q.AsSpan());
        return TryReadDocument(request.Get_IdView(), out var input)
            ? await Store.SaveRingtoneAsync(userId.Value, authKeyId, input,
                request.Unsave)
            : DocumentError();
    }
}
