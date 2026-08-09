// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class GetWebAuthorizationsHandler : AccountSettingsHandlerBase
{
    public GetWebAuthorizationsHandler(AccountSettingsStore store) : base(store) { }

    [TLFunction(Constructors.baseLayer_GetWebAuthorizations)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        if (await GetUserIdAsync(authKeyId) is null) return AuthError();
        return WebAuthorizations.Builder().Authorizations(new Vector())
            .Users(new Vector()).Build().TLBytes!.Value;
    }
}
