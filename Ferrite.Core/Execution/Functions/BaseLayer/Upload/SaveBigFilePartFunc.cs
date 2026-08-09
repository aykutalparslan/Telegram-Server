// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Services;
using Ferrite.Core.Execution;
using Ferrite.TL;
using Ferrite.TL.baseLayer.upload;

namespace Ferrite.Core.Execution.Functions.BaseLayer.Upload;

[TLFunction(Constructors.baseLayer_SaveBigFilePart)]
public class SaveBigFilePartFunc : ITLStreamingFunction
{
    private readonly IUploadService _uploadService;

    public SaveBigFilePartFunc(IUploadService uploadService)
    {
        _uploadService = uploadService;
    }

    public async ValueTask<TLBytes?> Process(ITLStreamingObject q, TLExecutionContext ctx)
    {
        var request = (SaveBigFilePart)q;
        var result = await _uploadService.SaveBigFilePart(request.FileId, request.FilePart,
            request.FileTotalParts, request.Bytes);
        return UploadRpcResult.Generate(result, ctx.MessageId);
    }
}
