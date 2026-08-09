// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class CancelPasswordEmailHandler
{
    private readonly IPasswordRecoveryService _recovery;

    public CancelPasswordEmailHandler(IPasswordRecoveryService recovery)
    {
        _recovery = recovery;
    }

    [TLFunction(Constructors.baseLayer_CancelPasswordEmail)]
    public ValueTask<TLBool> Handle(long authKeyId, TLBytes q) =>
        _recovery.CancelEmailAsync(authKeyId);
}
