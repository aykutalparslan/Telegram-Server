// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;
using Ferrite.TL.baseLayer.auth;
using TLAuthorization = Ferrite.TL.baseLayer.auth.TLAuthorization;

namespace Ferrite.Services.Handlers.AuthMethods;

public sealed class RecoverPasswordHandler
{
    private readonly IPasswordRecoveryService _recovery;

    public RecoverPasswordHandler(IPasswordRecoveryService recovery)
    {
        _recovery = recovery;
    }

    [TLFunction(Constructors.baseLayer_RecoverPassword)]
    public ValueTask<TLAuthorization> Handle(long authKeyId, TLBytes q)
    {
        var request = new RecoverPassword(q);
        string code = Encoding.UTF8.GetString(request.Code);
        TLPasswordInputSettings? settings = request.Flags[0]
            ? request.Get_NewSettings()
            : null;
        return _recovery.RecoverAsync(authKeyId, code, settings);
    }
}
