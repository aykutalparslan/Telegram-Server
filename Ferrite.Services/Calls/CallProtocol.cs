// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

public sealed record CallProtocol(bool UdpP2p, bool UdpReflector, int MinLayer,
    int MaxLayer, IReadOnlyList<string> LibraryVersions);
