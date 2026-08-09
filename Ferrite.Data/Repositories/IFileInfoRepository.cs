// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IFileInfoRepository
{
    public TLUploadedFileInfo? GetFileInfo(long fileId);
    public bool PutFileInfo(TLUploadedFileInfo uploadedFile);
    public bool PutBigFileInfo(TLUploadedFileInfo uploadedFile);
    public TLUploadedFileInfo? GetBigFileInfo(long fileId);
    public bool PutFilePart(TLFilePart part);
    public bool PutBigFilePart(TLFilePart part);
    public TLFilePart? GetFilePart(long fileId, int partNum);
    public TLFilePart? GetBigFilePart(long fileId, int partNum);
    public IReadOnlyCollection<TLFilePart> GetFileParts(long fileId);
    public bool SaveBigFilePart(TLFilePart part);
    public IReadOnlyCollection<TLFilePart> GetBigFileParts(long fileId);
    public bool PutUploadState(TLUploadPartState state);
    public TLUploadPartState? GetUploadState(long fileId, bool isBigFile);
    public bool PutFileReference(TLFileReference reference);
    public TLFileReference? GetFileReference(byte[] referenceBytes);
}