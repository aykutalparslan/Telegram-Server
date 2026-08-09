// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Core.Execution;
using Ferrite.TL;

namespace Ferrite.Core.Execution.Functions;

/// <summary>
/// A function whose successful result is a streamed download (<see cref="IFileOwner"/>)
/// rather than a buffered <see cref="TLBytes"/>. Failures carry one raw RpcError;
/// the request processor wraps it in rpc_result exactly once at the send boundary.
/// </summary>
public interface ITLFileFunction
{
    ValueTask<FileResult> Process(TLBytes q, TLExecutionContext ctx);
}

public readonly record struct FileResult(IFileOwner? File, TLBytes? Error);
