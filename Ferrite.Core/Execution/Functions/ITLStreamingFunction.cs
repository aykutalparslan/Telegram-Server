// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Core.Execution;
using Ferrite.TL;

namespace Ferrite.Core.Execution.Functions;

public interface ITLStreamingFunction
{
    ValueTask<TLBytes?> Process(ITLStreamingObject q, TLExecutionContext ctx);
}
