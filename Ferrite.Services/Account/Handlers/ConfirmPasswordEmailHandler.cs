// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class ConfirmPasswordEmailHandler
{
    private readonly IPasswordRecoveryService _recovery;

    public ConfirmPasswordEmailHandler(IPasswordRecoveryService recovery)
    {
        _recovery = recovery;
    }

    [TLFunction(Constructors.baseLayer_ConfirmPasswordEmail)]
    public ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = new ConfirmPasswordEmail(q.AsSpan());
        return _recovery.ConfirmEmailAsync(authKeyId,
            Encoding.UTF8.GetString(request.Code));
    }
}
