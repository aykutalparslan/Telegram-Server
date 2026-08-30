// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Core.Execution;
using Ferrite.TL;

namespace Ferrite.Core.Execution.Functions.BaseLayer;

public class NotImplementedFunc : ITLFunction
{
    public ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        using var error = RpcErrorGenerator.GenerateError(501, "METHOD_NOT_IMPLEMENTED"u8);
        return ValueTask.FromResult<TLBytes?>(RpcResultGenerator.Generate(error, ctx.MessageId));
    }
}
