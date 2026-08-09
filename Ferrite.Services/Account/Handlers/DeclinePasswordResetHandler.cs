// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class DeclinePasswordResetHandler
{
    private readonly IPasswordResetService _passwordReset;

    public DeclinePasswordResetHandler(IPasswordResetService passwordReset)
    {
        _passwordReset = passwordReset;
    }

    [TLFunction(Constructors.baseLayer_DeclinePasswordReset)]
    public ValueTask<TLBool> Handle(long authKeyId, TLBytes q) =>
        _passwordReset.DeclineAsync(authKeyId);
}
