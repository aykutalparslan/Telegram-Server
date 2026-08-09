// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class PollsRepository : IPollsRepository
{
    private readonly IKVStore _polls;
    private readonly IKVStore _votes;

    public PollsRepository(IKVStore polls, IKVStore votes)
    {
        _polls = polls;
        _votes = votes;
        polls.SetSchema(new TableDefinition("ferrite", "polls",
            new KeyDefinition("pk",
                new DataColumn { Name = "box_type", Type = DataType.Int },
                new DataColumn { Name = "box_id", Type = DataType.Long },
                new DataColumn { Name = "message_id", Type = DataType.Int })));
        votes.SetSchema(new TableDefinition("ferrite", "poll_votes",
            new KeyDefinition("pk",
                new DataColumn { Name = "poll_id", Type = DataType.Long },
                new DataColumn { Name = "user_id", Type = DataType.Long })));
    }

    public bool PutPoll(TLPollState poll)
    {
        var state = poll.AsPollState();
        return _polls.Put(poll.AsSpan().ToArray(), state.BoxType, state.BoxId,
            state.MessageId);
    }

    public async ValueTask<TLPollState?> GetPollAsync(MessageIdentity identity)
    {
        byte[]? bytes = await _polls.GetAsync(identity.BoxType, identity.BoxId,
            identity.MessageId);
        return bytes is { Length: > 0 }
            ? new TLPollState(bytes, 0, bytes.Length)
            : null;
    }

    public bool DeletePoll(MessageIdentity identity) =>
        _polls.Delete(identity.BoxType, identity.BoxId, identity.MessageId);

    public bool PutVote(TLPollVote vote)
    {
        var row = vote.AsPollVote();
        return _votes.Put(vote.AsSpan().ToArray(), row.PollId, row.UserId);
    }

    public async ValueTask<TLPollVote?> GetVoteAsync(long pollId, long userId)
    {
        byte[]? bytes = await _votes.GetAsync(pollId, userId);
        return bytes is { Length: > 0 }
            ? new TLPollVote(bytes, 0, bytes.Length)
            : null;
    }

    public async ValueTask<IReadOnlyCollection<TLPollVote>> GetVotesAsync(long pollId)
    {
        List<TLPollVote> votes = new();
        await foreach (byte[] bytes in _votes.IterateAsync(pollId))
        {
            votes.Add(new TLPollVote(bytes, 0, bytes.Length));
        }
        return votes;
    }

    public bool DeleteVote(long pollId, long userId) => _votes.Delete(pollId, userId);

    public bool DeleteVotes(long pollId) => _votes.Delete(pollId);
}
