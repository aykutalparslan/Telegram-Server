// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Models;

namespace Ferrite.Data.Search;

public sealed record MessageCandidateQuery(long? UserId, int? PeerType, long? PeerId,
    string? Text, int Limit);
