// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Services;
using Ferrite.Core.Execution;
using Ferrite.TL;

namespace Ferrite.Core.Execution.Functions.BaseLayer.Upload;

[TLFunction(Constructors.baseLayer_GetFile)]
public class GetFileFunc : ITLFileFunction
{
    private readonly IUploadService _uploadService;

    public GetFileFunc(IUploadService uploadService)
    {
        _uploadService = uploadService;
    }

    public async ValueTask<FileResult> Process(TLBytes q, TLExecutionContext ctx)
    {
        var result = await _uploadService.GetFile(ctx.CurrentAuthKeyId, q, ctx.MessageId);
        if (result.Success)
        {
            return new FileResult(result.Result, null);
        }

        return new FileResult(null, RpcErrorGenerator.GenerateError(
            result.ErrorMessage.Code, Encoding.UTF8.GetBytes(result.ErrorMessage.Message)));
    }
}
