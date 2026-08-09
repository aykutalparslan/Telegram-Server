// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data;

using Ferrite.TL.baseLayer.dto;

public class LocalFileOwner : IFileOwner
{
    private readonly long _fileId;
    private readonly int _parts;
    private readonly int _partSize;
    private readonly bool _isBigFile;
    private readonly IObjectStore _objectStore;
    private readonly long _offset;
    private readonly int _limit;

    public LocalFileOwner(TLUploadedFileInfo fileInfo, IObjectStore objectStore,
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
        long offset = _offset;
        Queue<Stream> streams = new Queue<Stream>();
        for (int i = 0; i < _parts; i++)
        {
            if (offset >= _partSize)
            {
                offset -= _partSize;
                continue;
            }
            if (_isBigFile)
            {
                var part = await _objectStore.GetBigFilePart(_fileId, i);
                streams.Enqueue(part);
            }
            else
            {
                var part = await _objectStore.GetFilePart(_fileId, i);
                streams.Enqueue(part);
            }
        }

        int streamOffset = streams.Count == 0 ? 0 : checked((int)offset);
        return new ConcatenatedStream(streams, streamOffset, _limit);
    }

    public long ReqMsgId { get; }
}
