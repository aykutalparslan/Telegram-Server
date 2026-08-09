// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Services;
using Ferrite.Core.Execution;
using Ferrite.TL;

namespace Ferrite.Core.Execution.Functions.BaseLayer.Auth;

[TLFunction(Constructors.baseLayer_SignIn)]
public class SignInFunc : ITLFunction
{
    private readonly IAuthService _auth;

    public SignInFunc(IAuthService auth)
    {
        _auth = auth;
    }
    public async ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        using var cancelCode = await _auth.SignIn(ctx.AuthKeyId, q);
        var rpcResult = RpcResultGenerator.Generate(cancelCode, ctx.MessageId);
        return rpcResult;
    }
}