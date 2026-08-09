// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using System.IO.Pipelines;
using Ferrite.Core.Execution;
using Ferrite.Services;
using Ferrite.TL;
using Ferrite.TL.baseLayer.upload;
using DotNext.IO.Pipelines;

namespace Ferrite.Core.Execution.Functions.BaseLayer.Upload;

/// <summary>
/// Buffered twin of <see cref="SaveBigFilePartFunc"/>; see
/// <see cref="SaveFilePartBufferedFunc"/> for why wrapped save-part requests
/// bypass the streaming path.
/// </summary>
[TLFunction(Constructors.baseLayer_SaveBigFilePart)]
public class SaveBigFilePartBufferedFunc : ITLFunction
{
    private readonly IUploadService _uploadService;

    public SaveBigFilePartBufferedFunc(IUploadService uploadService)
    {
        _uploadService = uploadService;
    }

    public async ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        var reader = PipeReader.Create(new ReadOnlySequence<byte>(q.AsSpan().ToArray()));
        _ = await reader.ReadInt32Async(true); // constructor, already dispatched
        var request = await SaveBigFilePart.ReadAsync(reader);
        var result = await _uploadService.SaveBigFilePart(request.FileId, request.FilePart,
            request.FileTotalParts, request.Bytes);
        return UploadRpcResult.Generate(result, ctx.MessageId);
    }
}
