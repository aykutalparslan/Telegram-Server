// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Core.Execution;
using Ferrite.TL;

namespace Ferrite.Core.Execution.Functions.BaseLayer.Auth;

[TLFunction(Constructors.baseLayer_ExportLoginToken)]
public class ExportLoginTokenFunc : ITLFunction
{
    private readonly IAuthService _auth;

    public ExportLoginTokenFunc(IAuthService auth)
    {
        _auth = auth;
    }

    public async ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        using var export = await _auth.ExportLoginToken(ctx.CurrentAuthKeyId, ctx.SessionId, q);
        var rpcResult = RpcResultGenerator.Generate(export, ctx.MessageId);
        return rpcResult;
    }
}