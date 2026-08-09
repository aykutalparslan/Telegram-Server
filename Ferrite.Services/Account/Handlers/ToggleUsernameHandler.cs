// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class ToggleUsernameHandler : ProfileSettingsHandlerBase
{
    public ToggleUsernameHandler(ProfileStore store) : base(store) { }

    [TLFunction(Constructors.baseLayer_AccountToggleUsername)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new AccountToggleUsername(q.AsSpan());
        return await Store.ToggleUsernameAsync(userId.Value,
            Encoding.UTF8.GetString(request.Username), request.Active);
    }
}
