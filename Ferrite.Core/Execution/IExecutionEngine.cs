// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Core.Execution.Functions;
using Ferrite.Core.Execution;
using Ferrite.TL;

namespace Ferrite.Core.Execution;

public interface IExecutionEngine
{
    protected const int DefaultLayer = 214;
    /// <summary>
    /// Invokes a Function with the specified layer.
    /// Function (functional combinator) is a combinator which may be computed (reduced)
    /// on condition that the requisite number of arguments of requisite types are provided.
    /// The result of the computation is an expression consisting of constructors
    /// and base type values only.
    /// </summary>
    /// <param name="rpc">Serialized functional combinator.</param>
    /// <param name="layer">Layer with which the function should be computed.</param>
    /// <returns>TL Serialized result of the computation.</returns>
    public ValueTask<TLBytes?> Invoke(TLBytes rpc, TLExecutionContext ctx, int layer = DefaultLayer);
    public ValueTask<TLBytes?> Invoke(ITLStreamingObject rpc, TLExecutionContext ctx, int layer = DefaultLayer);
    /// <summary>
    /// Invokes a download function whose success result is a streamed
    /// <see cref="Ferrite.Data.IFileOwner"/> rather than a buffered TLBytes.
    /// </summary>
    public ValueTask<FileResult> InvokeFile(TLBytes rpc, TLExecutionContext ctx, int layer = DefaultLayer);
    /// <summary>
    /// Returns whether the request is an upload.getFile call, including when
    /// it is nested in Telegram's standard invocation wrappers.
    /// </summary>
    public bool IsFileRequest(TLBytes rpc);
    public bool IsImplemented(int constructor, int layer = DefaultLayer);
}
