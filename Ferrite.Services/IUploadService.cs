// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

public interface IUploadService
{
    public Task<ServiceResult<bool>> SaveFilePart(long fileId, int filePart, Stream data);
    public Task<ServiceResult<bool>> SaveBigFilePart(long fileId, int filePart, int fileTotalParts, Stream data);
    public Task<ServiceResult<TLUploadedFileInfo?>> SaveFile(TLInputFile file);
    public Task<ServiceResult<TLUploadedFileInfo?>> SaveEncryptedFile(TLInputFile file);
    public Task<ServiceResult<TLBytes?>> RegisterDocument(TLUploadedFileInfo finalized,
        byte[] mimeType, byte[] attributesVectorBytes, byte[]? thumbsVectorBytes);
    public Task<ServiceResult<IFileOwner>> GetFile(long authKeyId, TLBytes request, long reqMsgId);
}
