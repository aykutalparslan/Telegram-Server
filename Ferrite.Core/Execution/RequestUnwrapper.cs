// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using DotNext.Buffers;
using Ferrite.TL;

namespace Ferrite.Core.Execution;

/// <summary>
/// Shared single-step unwrap primitives for Telegram's invocation wrappers
/// (invokeWithLayer / initConnection / invokeAfterMsg(s) / invokeWithoutUpdates /
/// invokeWithMessagesRange / invokeWithGooglePlayIntegrity /
/// invokeWithTakeout / invokeWithApnsSecret / invokeWithReCaptcha).
/// One implementation is used by the buffered wrapper handlers, the engine file
/// path (<see cref="ExecutionEngine.InvokeFile"/>) and file detection
/// (<see cref="ExecutionEngine.IsFileRequest"/>), so the query extraction is not
/// re-implemented per call site. gzip is handled separately by GzipPackedHelper.
/// The initConnection SaveAppInfo side effect stays with the caller
/// (InitConnectionFunc / InvokeFile); this type only extracts the inner query
/// bytes. Each result owns pooled memory; the caller disposes it.
/// </summary>
internal static class RequestUnwrapper
{
    /// <summary>Rents pooled memory and copies an inner query span into an owned TLBytes.</summary>
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

    // These declarations share their constructor ids with the generic wrappers.
    // The generator therefore emits the prefix views only; ReadSize measures the
    // prefix and the generic query occupies the remaining bytes.
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
