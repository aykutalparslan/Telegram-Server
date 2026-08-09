// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Core.Execution;
using Ferrite.TL;

namespace Ferrite.Core.Execution.Functions.BaseLayer;

[TLFunction(Constructors.baseLayer_InvokeWithLayer)]
public class InvokeWithLayerFunc : ITLFunction
{
    public IExecutionEngine? ExecutionEngine { get; set; }
    public async ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        using var query = RequestUnwrapper.InvokeWithLayerQuery(q, out int layer);
        return ExecutionEngine == null ? null : await ExecutionEngine.Invoke(query, ctx, layer);
    }
}
