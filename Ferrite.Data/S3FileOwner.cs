// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data;

using Ferrite.TL.baseLayer.dto;

public class S3FileOwner : IFileOwner
{
    private readonly long _fileId;
    private readonly int _parts;
    private readonly int _partSize;
    private readonly bool _isBigFile;
    private readonly IObjectStore _objectStore;
    private readonly long _offset;
    private readonly int _limit;

    public S3FileOwner(TLUploadedFileInfo fileInfo, IObjectStore objectStore,
        long offset, int limit, long reqMsgId, byte[] streamHeader)
    {
        var info = fileInfo.AsUploadedFileInfo();
        _fileId = info.Id;
        _parts = info.Parts;
        _partSize = info.PartSize;
        _isBigFile = info.IsBigFile;
        TLObjectHeader = streamHeader;
        _objectStore = objectStore;
        _offset = offset;
        _limit = limit;
        ReqMsgId = reqMsgId;
    }

    public byte[] TLObjectHeader { get; init; }

    public async ValueTask<Stream> GetFileStream()
    {
        if (_parts <= 0)
        {
            return Stream.Null;
        }

        // Only the final part can be shorter than PartSize. Probe it once for
        // the exact logical length, then let the lazy stream fetch one part at
        // a time. Eagerly opening every S3 response exhausts the HTTP pool for
        // multi-megabyte files, especially because MTProto reads once for the
        // message key and once for transmission.
        await using Stream lastPart = await GetPart(_parts - 1);
        long totalLength = checked((long)(_parts - 1) * _partSize + lastPart.Length);
        long available = Math.Max(0, totalLength - _offset);
        long length = Math.Min(available, Math.Max(0, _limit));
        return new ObjectStorePartStream(_objectStore, _fileId, _parts, _partSize,
            _isBigFile, _offset, length);
    }

    private ValueTask<Stream> GetPart(int part)
    {
        return _isBigFile
            ? _objectStore.GetBigFilePart(_fileId, part)
            : _objectStore.GetFilePart(_fileId, part);
    }

    public long ReqMsgId { get; }
}
