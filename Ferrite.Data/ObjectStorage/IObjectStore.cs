// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.ObjectStorage;

public interface IObjectStore
{
    public ValueTask<bool> SaveFilePart(long fileId, int filePart, Stream data);
    public ValueTask<bool> SaveBigFilePart(long fileId, int filePart, int fileTotalParts, Stream data);
    public ValueTask<Stream> GetFilePart(long fileId, int filePart);
    public ValueTask<Stream> GetBigFilePart(long fileId, int filePart);
    public IFileOwner GetFileOwner(TLUploadedFileInfo fileInfo, long offset,
        int limit, long reqMsgId, byte[] fileHeaders);
}
