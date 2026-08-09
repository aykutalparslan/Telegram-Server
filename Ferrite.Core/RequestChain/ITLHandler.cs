// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Core.Execution;
using Ferrite.TL;

namespace Ferrite.Core.RequestChain;

public interface ITLHandler
{
    public ValueTask Process(object? sender, TLBytes input, TLExecutionContext ctx);
    public ValueTask Process(object? sender, ITLStreamingObject input, TLExecutionContext ctx);
}
