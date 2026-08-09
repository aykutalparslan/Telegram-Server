// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.auth;

namespace Ferrite.Services.Handlers.AuthMethods;

public sealed class RequestPasswordRecoveryHandler
{
    private readonly IPasswordRecoveryService _recovery;

    public RequestPasswordRecoveryHandler(IPasswordRecoveryService recovery)
    {
        _recovery = recovery;
    }

    [TLFunction(Constructors.baseLayer_RequestPasswordRecovery)]
    public ValueTask<TLPasswordRecovery> Handle(long authKeyId, TLBytes q) =>
        _recovery.RequestAsync(authKeyId);
}
