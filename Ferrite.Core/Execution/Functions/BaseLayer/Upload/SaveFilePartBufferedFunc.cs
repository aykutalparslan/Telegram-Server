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
/// Buffered twin of <see cref="SaveFilePartFunc"/>. The frame decoder can only
/// stream a save-part request whose constructor is the first word of the
/// message body; TDLib wraps the first request on a fresh upload connection in
/// invokeWithLayer/initConnection, so such parts arrive fully buffered through
/// the generic invoke path instead.
/// </summary>
[TLFunction(Constructors.baseLayer_SaveFilePart)]
public class SaveFilePartBufferedFunc : ITLFunction
{
    private readonly IUploadService _uploadService;

    public SaveFilePartBufferedFunc(IUploadService uploadService)
    {
        _uploadService = uploadService;
    }

    public async ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        var reader = PipeReader.Create(new ReadOnlySequence<byte>(q.AsSpan().ToArray()));
        _ = await reader.ReadInt32Async(true); // constructor, already dispatched
        var request = await SaveFilePart.ReadAsync(reader);
        var result = await _uploadService.SaveFilePart(request.FileId, request.FilePart,
            request.Bytes);
        return UploadRpcResult.Generate(result, ctx.MessageId);
    }
}
