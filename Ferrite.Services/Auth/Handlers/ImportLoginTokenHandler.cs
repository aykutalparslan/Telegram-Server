// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.auth;

namespace Ferrite.Services.Handlers.AuthMethods;

public sealed class ImportLoginTokenHandler
{
    private readonly ILoginTokenService _loginTokens;

    public ImportLoginTokenHandler(ILoginTokenService loginTokens)
    {
        _loginTokens = loginTokens;
    }

    [TLFunction(Constructors.baseLayer_ImportLoginToken)]
    public ValueTask<TLLoginToken> Handle(long authKeyId, TLBytes q)
    {
        var request = new ImportLoginToken(q.AsSpan());
        return _loginTokens.ImportAsync(authKeyId, request.Token.ToArray());
    }
}
