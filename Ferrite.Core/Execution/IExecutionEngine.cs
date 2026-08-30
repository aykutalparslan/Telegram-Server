// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Core.Execution.Functions;
using Ferrite.Core.Execution;
using Ferrite.TL;

namespace Ferrite.Core.Execution;

public interface IExecutionEngine
{
    protected const int DefaultLayer = 214;
    public ValueTask<TLBytes?> Invoke(TLBytes rpc, TLExecutionContext ctx, int layer = DefaultLayer);
    public ValueTask<TLBytes?> Invoke(ITLStreamingObject rpc, TLExecutionContext ctx, int layer = DefaultLayer);
    public ValueTask<FileResult> InvokeFile(TLBytes rpc, TLExecutionContext ctx, int layer = DefaultLayer);
    public bool IsFileRequest(TLBytes rpc);
    public bool IsImplemented(int constructor, int layer = DefaultLayer);
}
