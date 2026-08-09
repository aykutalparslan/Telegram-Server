// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class ResetWebAuthorizationsHandler : AccountSettingsHandlerBase
{
    public ResetWebAuthorizationsHandler(AccountSettingsStore store) : base(store) { }

    [TLFunction(Constructors.baseLayer_ResetWebAuthorizations)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q) =>
        await GetUserIdAsync(authKeyId) is { }
            ? BoolTrue.Builder().Build().TLBytes!.Value : AuthError();
}

