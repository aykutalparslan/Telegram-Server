// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

/// <summary>
/// Canonical poll definitions and the individual vote rows they are counted
/// from. A poll row is keyed by the <see cref="MessageIdentity"/> of the message
/// whose media carries it, so every per-owner copy of one logical message shares
/// a single poll. Vote rows are keyed by poll and voter, which is what makes a
/// re-vote a replacement rather than a second ballot.
///
/// Results are deliberately NOT stored. `pollResults` differs per viewer (the
/// `chosen` flags and the `min` gate that hides quiz answers), and every field
/// of it is derivable from the vote rows, so deriving it removes the only place
/// a stored aggregate could drift from the ballots it claims to summarize.
/// </summary>
public interface IPollsRepository
{
    bool PutPoll(TLPollState poll);
    ValueTask<TLPollState?> GetPollAsync(MessageIdentity identity);
    bool DeletePoll(MessageIdentity identity);

    bool PutVote(TLPollVote vote);
    ValueTask<TLPollVote?> GetVoteAsync(long pollId, long userId);
    ValueTask<IReadOnlyCollection<TLPollVote>> GetVotesAsync(long pollId);
    bool DeleteVote(long pollId, long userId);
    bool DeleteVotes(long pollId);
}
