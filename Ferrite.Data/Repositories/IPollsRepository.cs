// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

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
