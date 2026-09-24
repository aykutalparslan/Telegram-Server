// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Core.Execution;
using Ferrite.TL;

namespace Ferrite.Core.Execution.Functions.BaseLayer;

[TLFunction(Constructors.baseLayer_InvokeWithLayer)]
public class InvokeWithLayerFunc : ITLFunction
{
    private readonly InitConnectionFunc _initConnection;

    public InvokeWithLayerFunc(InitConnectionFunc initConnection)
    {
        _initConnection = initConnection;
    }

    public async ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        LayerNegotiationResult negotiation = ConnectionLayerNegotiator.Resolve(q);
        if (!negotiation.IsSuccess)
        {
            return ConnectionLayerNegotiator.WrappedError(negotiation.Failure, ctx.MessageId);
        }

        using var query = RequestUnwrapper.InvokeWithLayerQuery(q, out _);
        return await _initConnection.Process(query, ctx, negotiation.ProvisionalLayer);
    }
}
