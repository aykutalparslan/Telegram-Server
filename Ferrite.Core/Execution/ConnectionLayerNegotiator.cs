// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Core.Execution.Functions;
using Ferrite.TL;

namespace Ferrite.Core.Execution;

internal enum LayerNegotiationFailure
{
    None,
    InvalidLayer,
    NotInitialized
}

internal readonly record struct LayerNegotiationResult(
    int ProvisionalLayer, LayerNegotiationFailure Failure)
{
    public bool IsSuccess => Failure == LayerNegotiationFailure.None;
}

internal static class ConnectionLayerNegotiator
{
    public static LayerNegotiationResult Resolve(TLBytes request)
    {
        if (request.Constructor != Constructors.baseLayer_InvokeWithLayer)
        {
            return new(0, LayerNegotiationFailure.NotInitialized);
        }

        try
        {
            using var query = RequestUnwrapper.InvokeWithLayerQuery(request, out int layer);
            if (query.Constructor != Constructors.baseLayer_InitConnection)
            {
                return new(0, LayerNegotiationFailure.NotInitialized);
            }

            return SupportedLayers.Contains(layer)
                ? new(layer, LayerNegotiationFailure.None)
                : new(0, LayerNegotiationFailure.InvalidLayer);
        }
        catch
        {
            return new(0, LayerNegotiationFailure.NotInitialized);
        }
    }

    public static TLBytes Error(LayerNegotiationFailure failure)
    {
        ReadOnlySpan<byte> message = failure == LayerNegotiationFailure.InvalidLayer
            ? "CONNECTION_LAYER_INVALID"u8
            : "CONNECTION_NOT_INITED"u8;
        return RpcErrorGenerator.GenerateError(400, message);
    }

    public static TLBytes WrappedError(LayerNegotiationFailure failure, long messageId)
    {
        using var error = Error(failure);
        return RpcResultGenerator.Generate(error, messageId);
    }
}
