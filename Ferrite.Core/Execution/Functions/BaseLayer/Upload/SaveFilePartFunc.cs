// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Core.Execution;
using Ferrite.Services;
using Ferrite.TL;
using Ferrite.TL.baseLayer.upload;

namespace Ferrite.Core.Execution.Functions.BaseLayer.Upload;

[TLFunction(Constructors.baseLayer_SaveFilePart)]
public class SaveFilePartFunc : ITLStreamingFunction
{
    private readonly IUploadService _uploadService;

    public SaveFilePartFunc(IUploadService uploadService)
    {
        _uploadService = uploadService;
    }

    public async ValueTask<TLBytes?> Process(ITLStreamingObject q, TLExecutionContext ctx)
    {
        var request = (SaveFilePart)q;
        var result = await _uploadService.SaveFilePart(request.FileId, request.FilePart, request.Bytes);
        return UploadRpcResult.Generate(result, ctx.MessageId);
    }
}

internal static class UploadRpcResult
{
    public static TLBytes Generate(ServiceResult<bool> result, long reqMsgId)
    {
        if (!result.Success)
        {
            using var error = RpcErrorGenerator.GenerateError(result.ErrorMessage.Code,
                Encoding.UTF8.GetBytes(result.ErrorMessage.Message));
            return RpcResultGenerator.Generate(error, reqMsgId);
        }

        if (result.Result)
        {
            using var boolTrue = new TL.BoolTrue();
            return RpcResultGenerator.Generate(boolTrue.TLBytes!.Value, reqMsgId);
        }

        using var boolFalse = new TL.BoolFalse();
        return RpcResultGenerator.Generate(boolFalse.TLBytes!.Value, reqMsgId);
    }
}
