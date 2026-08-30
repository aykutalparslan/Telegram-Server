// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Core.Execution;
using Ferrite.TL;

namespace Ferrite.Core.Execution.Functions.BaseLayer.Auth;

[TLFunction(Constructors.baseLayer_SignUp)]
public class SignUpFunc : ITLFunction
{
    private readonly IAuthService _auth;

    public SignUpFunc(IAuthService auth)
    {
        _auth = auth;
    }
    public async ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        using var cancelCode = await _auth.SignUp(ctx.CurrentAuthKeyId, q);
        var rpcResult = RpcResultGenerator.Generate(cancelCode, ctx.MessageId);
        return rpcResult;
    }
}