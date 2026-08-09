// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Search;

/// <summary>
/// A scoped candidate lookup against the message index. The index only narrows
/// what has to be hydrated; the durable message repositories stay authoritative,
/// so a hit here is a candidate and never a result on its own.
/// </summary>
/// <param name="UserId">
/// Owner of the message box being searched. Absent means every box, which is
/// what a search that reaches beyond the caller's own conversations needs:
/// public-post search has to discover WHICH channels carry a match before it can
/// name one.
/// </param>
/// <param name="PeerType">Optional conversation type to restrict to.</param>
/// <param name="PeerId">Optional conversation id to restrict to.</param>
/// <param name="Text">Optional free-text query; absent means "every row".</param>
/// <param name="Limit">Maximum candidates to return; non-positive means default.</param>
public sealed record MessageCandidateQuery(long? UserId, int? PeerType, long? PeerId,
    string? Text, int Limit);
