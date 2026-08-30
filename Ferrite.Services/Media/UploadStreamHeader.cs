// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.storage;
using Ferrite.TL.baseLayer.upload;
using Ferrite.TL.mtproto;

namespace Ferrite.Services.Media;

public static class UploadStreamHeader
{
    public static byte[] GenerateStreamHeader(long reqMsgId, StreamFileType fileType, int mtime)
    {
        var typeBytes = SerializeFileType(fileType);
        using var file = UploadFile.Builder()
            .Type(typeBytes)
            .Mtime(mtime)
            .Bytes(ReadOnlySpan<byte>.Empty)
            .Build();
        using var rpcResult = RpcResult.Builder()
            .ReqMsgId(reqMsgId)
            .Result(file.ToReadOnlySpan())
            .Build();
        var serialized = rpcResult.ToReadOnlySpan();
        return serialized[..^4].ToArray();
    }

    private static byte[] SerializeFileType(StreamFileType fileType)
    {
        switch (fileType)
        {
            case StreamFileType.Gif:
            {
                using var value = new FileGif();
                return value.ToReadOnlySpan().ToArray();
            }
            case StreamFileType.Jpeg:
            {
                using var value = new FileJpeg();
                return value.ToReadOnlySpan().ToArray();
            }
            case StreamFileType.Mov:
            {
                using var value = new FileMov();
                return value.ToReadOnlySpan().ToArray();
            }
            case StreamFileType.Mp3:
            {
                using var value = new FileMp3();
                return value.ToReadOnlySpan().ToArray();
            }
            case StreamFileType.Mp4:
            {
                using var value = new FileMp4();
                return value.ToReadOnlySpan().ToArray();
            }
            case StreamFileType.Partial:
            {
                using var value = new FilePartial();
                return value.ToReadOnlySpan().ToArray();
            }
            case StreamFileType.Png:
            {
                using var value = new FilePng();
                return value.ToReadOnlySpan().ToArray();
            }
            case StreamFileType.Webp:
            {
                using var value = new FileWebp();
                return value.ToReadOnlySpan().ToArray();
            }
            default:
            {
                using var value = new FileUnknown();
                return value.ToReadOnlySpan().ToArray();
            }
        }
    }
}
