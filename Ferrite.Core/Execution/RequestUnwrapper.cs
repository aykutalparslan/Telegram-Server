// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using DotNext.Buffers;
using Ferrite.TL;

namespace Ferrite.Core.Execution;

internal static class RequestUnwrapper
{
    public static TLBytes CopyQuery(ReadOnlySpan<byte> query)
    {
        var queryMemory = UnmanagedMemoryPool<byte>.Shared.Rent(query.Length);
        query.CopyTo(queryMemory.Memory.Span);
        return new TLBytes(queryMemory, 0, query.Length);
    }

    public static TLBytes InvokeWithLayerQuery(TLBytes rpc, out int layer)
    {
        var request = new TL.baseLayer.InvokeWithLayer(rpc.AsSpan());
        layer = request.Layer;
        return CopyQuery(request.Query);
    }

    public static TLBytes InitConnectionQuery(TLBytes rpc)
        => CopyQuery(new TL.baseLayer.InitConnection(rpc.AsSpan()).Query);

    public static TLBytes InvokeAfterMsgQuery(TLBytes rpc)
        => CopyQuery(new TL.baseLayer.InvokeAfterMsg(rpc.AsSpan()).Query);

    public static TLBytes InvokeAfterMsgsQuery(TLBytes rpc)
        => CopyQuery(new TL.baseLayer.InvokeAfterMsgs(rpc.AsSpan()).Query);

    public static TLBytes InvokeWithoutUpdatesQuery(TLBytes rpc)
        => CopyQuery(new TL.baseLayer.InvokeWithoutUpdates(rpc.AsSpan()).Query);

    public static TLBytes InvokeWithMessagesRangeQuery(TLBytes rpc)
        => CopyQuery(new TL.baseLayer.InvokeWithMessagesRange(rpc.AsSpan()).Query);

    public static TLBytes InvokeWithTakeoutQuery(TLBytes rpc, out long takeoutId)
    {
        var request = new TL.baseLayer.InvokeWithTakeout(rpc.AsSpan());
        takeoutId = request.TakeoutId;
        return CopyQuery(request.Query);
    }

    public static TLBytes InvokeWithGooglePlayIntegrityQuery(TLBytes rpc)
    {
        var span = rpc.AsSpan();
        return CopyQuery(span[TL.baseLayer.InvokeWithGooglePlayIntegrityPrefix
            .ReadSize(span, 0)..]);
    }

    public static TLBytes InvokeWithApnsSecretQuery(TLBytes rpc)
    {
        var span = rpc.AsSpan();
        return CopyQuery(span[TL.baseLayer.InvokeWithApnsSecretPrefix
            .ReadSize(span, 0)..]);
    }

    public static TLBytes InvokeWithReCaptchaQuery(TLBytes rpc)
    {
        var span = rpc.AsSpan();
        return CopyQuery(span[TL.baseLayer.InvokeWithReCaptchaPrefix
            .ReadSize(span, 0)..]);
    }
}
