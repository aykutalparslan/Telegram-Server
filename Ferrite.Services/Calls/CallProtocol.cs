// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

/// <summary>
/// In-memory representation of a phoneCallProtocol offer or negotiation
/// result. This is transient call-session state, never a persisted row.
/// </summary>
public sealed record CallProtocol(bool UdpP2p, bool UdpReflector, int MinLayer,
    int MaxLayer, IReadOnlyList<string> LibraryVersions);
