// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class ResetPasswordHandler
{
    private readonly IPasswordResetService _passwordReset;

    public ResetPasswordHandler(IPasswordResetService passwordReset)
    {
        _passwordReset = passwordReset;
    }

    [TLFunction(Constructors.baseLayer_ResetPassword)]
    public ValueTask<TLResetPasswordResult> Handle(long authKeyId, TLBytes q) =>
        _passwordReset.ResetAsync(authKeyId);
}
