// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Core.Execution.Functions.BaseLayer;

/// <summary>
/// Layer-214 methods Ferrite intends to implement in a later sub-phase.
/// Each entry names its owning sub-phase, so a sub-phase's worklist is exactly the
/// entries it must delete as it adds real handlers. Compare with
/// <see cref="DisabledMethods"/>, which is permanent refusal.
///
/// THE BUCKET IS EMPTY, and has been since landed the `stats.*` slice:
/// every layer-214 method is now either a real handler or an explicit permanent
/// refusal, and nothing answers 501 METHOD_NOT_IMPLEMENTED. The type stays
/// because the machinery around it — the ledger assertion, the dispatch
/// registration and the sub-phase matrices — is what keeps that true.
/// </summary>
public static class NotImplementedMethods
{
    public static readonly FunctionKey[] Keys = [];
}
