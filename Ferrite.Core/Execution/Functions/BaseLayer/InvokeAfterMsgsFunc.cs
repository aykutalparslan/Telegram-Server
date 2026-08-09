// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Core.Execution;
using Ferrite.TL;

namespace Ferrite.Core.Execution.Functions.BaseLayer;

[TLFunction(Constructors.baseLayer_InvokeAfterMsgs)]
public class InvokeAfterMsgsFunc : ITLFunction
{
    public IExecutionEngine? ExecutionEngine { get; set; }

    public async ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        using var query = RequestUnwrapper.InvokeAfterMsgsQuery(q);
        return ExecutionEngine == null ? null : await ExecutionEngine.Invoke(query, ctx);
    }
}
