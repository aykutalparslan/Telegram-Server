// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers.Binary;
using DotNext.Buffers;
using Ferrite.Core.RequestChain;
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
        TLBytes query = CopyQuery(request.Query);
        if (query.Constructor == LegacyConstructors.InitConnection)
        {
            BinaryPrimitives.WriteInt32LittleEndian(query.AsSpan(),
                Constructors.baseLayer_InitConnection);
        }
        return query;
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

    public static int MethodConstructor(TLBytes rpc)
    {
        if (rpc.Constructor == Constructors.mtproto_GzipPacked)
        {
            using var unpacked = GzipPackedHelper.Unpack(rpc);
            return MethodConstructor(unpacked);
        }
        if (rpc.Constructor == Constructors.baseLayer_InvokeWithLayer)
        {
            using var query = InvokeWithLayerQuery(rpc, out _);
            return MethodConstructor(query);
        }
        if (rpc.Constructor == Constructors.baseLayer_InitConnection)
        {
            using var query = InitConnectionQuery(rpc);
            return MethodConstructor(query);
        }
        if (rpc.Constructor == Constructors.baseLayer_InvokeAfterMsg)
        {
            using var query = InvokeAfterMsgQuery(rpc);
            return MethodConstructor(query);
        }
        if (rpc.Constructor == Constructors.baseLayer_InvokeAfterMsgs)
        {
            using var query = InvokeAfterMsgsQuery(rpc);
            return MethodConstructor(query);
        }
        if (rpc.Constructor == Constructors.baseLayer_InvokeWithoutUpdates)
        {
            using var query = InvokeWithoutUpdatesQuery(rpc);
            return MethodConstructor(query);
        }
        if (rpc.Constructor == Constructors.baseLayer_InvokeWithMessagesRange)
        {
            using var query = InvokeWithMessagesRangeQuery(rpc);
            return MethodConstructor(query);
        }
        if (rpc.Constructor == Constructors.baseLayer_InvokeWithTakeout)
        {
            using var query = InvokeWithTakeoutQuery(rpc, out _);
            return MethodConstructor(query);
        }
        if (rpc.Constructor == Constructors.baseLayer_InvokeWithGooglePlayIntegrityPrefix)
        {
            using var query = InvokeWithGooglePlayIntegrityQuery(rpc);
            return MethodConstructor(query);
        }
        if (rpc.Constructor == Constructors.baseLayer_InvokeWithApnsSecretPrefix)
        {
            using var query = InvokeWithApnsSecretQuery(rpc);
            return MethodConstructor(query);
        }
        if (rpc.Constructor == Constructors.baseLayer_InvokeWithReCaptchaPrefix)
        {
            using var query = InvokeWithReCaptchaQuery(rpc);
            return MethodConstructor(query);
        }
        return rpc.Constructor;
    }
}
