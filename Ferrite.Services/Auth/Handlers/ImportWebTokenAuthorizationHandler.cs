// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer.auth;

namespace Ferrite.Services.Handlers.AuthMethods;

public sealed class ImportWebTokenAuthorizationHandler
{
    private readonly ILoginTokenService _loginTokens;

    public ImportWebTokenAuthorizationHandler(ILoginTokenService loginTokens)
    {
        _loginTokens = loginTokens;
    }

    [TLFunction(Constructors.baseLayer_ImportWebTokenAuthorization)]
    public ValueTask<TLAuthorization> Handle(long authKeyId, TLBytes q)
    {
        var request = new ImportWebTokenAuthorization(q.AsSpan());
        return _loginTokens.ImportWebAsync(authKeyId, request.ApiId,
            Encoding.UTF8.GetString(request.ApiHash),
            Encoding.UTF8.GetString(request.WebAuthToken));
    }
}
