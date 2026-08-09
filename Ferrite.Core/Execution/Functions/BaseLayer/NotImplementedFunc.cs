// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Services;
using Ferrite.Core.Execution;
using Ferrite.TL;

namespace Ferrite.Core.Execution.Functions.BaseLayer;

/// <summary>
/// Answers a layer-214 method Ferrite intends to implement in a later /// sub-phase but has not implemented yet. Distinct from <see cref="DisabledFunc"/>,
/// which permanently refuses a surface Ferrite will never serve.
///
/// The code MUST stay out of {negative, 500, 420, 303}: the pinned client routes
/// 500 and negative codes to NetQueryDelayer (NetQueryDispatcher.cpp:102), which
/// resends with a doubling timeout until the total limit is passed and then
/// rewrites the error to a fabricated 429. 501 passes straight through.
/// </summary>
public class NotImplementedFunc : ITLFunction
{
    public ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        using var error = RpcErrorGenerator.GenerateError(501, "METHOD_NOT_IMPLEMENTED"u8);
        return ValueTask.FromResult<TLBytes?>(RpcResultGenerator.Generate(error, ctx.MessageId));
    }
}
