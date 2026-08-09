// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Services;
using Ferrite.Core.Execution;
using Ferrite.TL;

namespace Ferrite.Core.Execution.Functions.BaseLayer.Auth;

[TLFunction(Constructors.baseLayer_BindTempAuthKey)]
public class BindTempAuthKeyFunc : ITLFunction
{
    private readonly IAuthService _auth;

    public BindTempAuthKeyFunc(IAuthService auth)
    {
        _auth = auth;
    }
    public async ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        using var bindResult = await _auth.BindTempAuthKey(ctx.SessionId, q);
        var rpcResult = RpcResultGenerator.Generate(bindResult, ctx.MessageId);
        return rpcResult;
    }
}