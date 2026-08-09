// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.auth;

namespace Ferrite.Services.Handlers.AuthMethods;

public sealed class CheckRecoveryPasswordHandler
{
    private readonly IPasswordRecoveryService _recovery;

    public CheckRecoveryPasswordHandler(IPasswordRecoveryService recovery)
    {
        _recovery = recovery;
    }

    [TLFunction(Constructors.baseLayer_CheckRecoveryPassword)]
    public ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = new CheckRecoveryPassword(q.AsSpan());
        return _recovery.CheckAsync(authKeyId,
            Encoding.UTF8.GetString(request.Code));
    }
}
