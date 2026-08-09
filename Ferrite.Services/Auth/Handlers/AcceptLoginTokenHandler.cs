// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.auth;

namespace Ferrite.Services.Handlers.AuthMethods;

public sealed class AcceptLoginTokenHandler
{
    private readonly ILoginTokenService _loginTokens;

    public AcceptLoginTokenHandler(ILoginTokenService loginTokens)
    {
        _loginTokens = loginTokens;
    }

    [TLFunction(Constructors.baseLayer_AcceptLoginToken)]
    public ValueTask<Ferrite.TL.baseLayer.TLAuthorization> Handle(
        long authKeyId, TLBytes q)
    {
        var request = new AcceptLoginToken(q.AsSpan());
        return _loginTokens.AcceptAsync(authKeyId, request.Token.ToArray());
    }
}
