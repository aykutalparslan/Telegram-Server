// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.auth;
using TLAuthorization = Ferrite.TL.baseLayer.auth.TLAuthorization;

namespace Ferrite.Services.Handlers.AuthMethods;

public sealed class CheckPasswordHandler
{
    private readonly IAccountPasswordManager _passwords;

    public CheckPasswordHandler(IAccountPasswordManager passwords)
    {
        _passwords = passwords;
    }

    [TLFunction(Constructors.baseLayer_CheckPassword)]
    public ValueTask<TLAuthorization> Handle(long authKeyId, TLBytes q)
    {
        var request = new CheckPassword(q.AsSpan());
        return _passwords.CheckPasswordAsync(authKeyId, request.Get_Password());
    }
}
