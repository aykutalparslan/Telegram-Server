// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Services;
using Ferrite.Core.Execution;
using Ferrite.TL;

namespace Ferrite.Core.Execution.Functions.BaseLayer;

public class DisabledFunc : ITLFunction
{
    public ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        using var error = RpcErrorGenerator.GenerateError(403, "METHOD_DISABLED"u8);
        return ValueTask.FromResult<TLBytes?>(RpcResultGenerator.Generate(error, ctx.MessageId));
    }
}
