// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

/// <summary>
/// The one poll state machine shared by the send paths, `messages.editMessage`,
/// `messages.sendVote`, `messages.getPollResults` and `messages.getPollVotes`.
///
/// Only the poll definition and the individual ballots are durable. `pollResults`
/// is always derived, because it is viewer-specific: pinned TDLib ignores the
/// `chosen` flags, the quiz `correct` flag and the solution whenever
/// `pollResults.min` is set and the poll is still open
/// (`PollManager.cpp:1751,1769,1776,1841`), so the same poll must answer one
/// viewer with `min` and another without it.
/// </summary>
public sealed class PollStore
{
    private readonly IPollsRepository _pollsRepository;

    /// Telegram's poll_answers_min/poll_answers_max defaults.
    public const int MinAnswers = 2;
    public const int MaxAnswers = 10;

    /// How many voter peers a public poll advertises before anyone opens it.
    public const int RecentVoterLimit = 3;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IdAllocators _ids;
    private readonly TimeProvider _timeProvider;

    public PollStore(IUnitOfWork unitOfWork, IPollsRepository pollsRepository, IdAllocators ids,
        TimeProvider timeProvider)
    {
        _pollsRepository = pollsRepository;

        _unitOfWork = unitOfWork;
        _ids = ids;
        _timeProvider = timeProvider;
    }

    // ---- await-safe snapshots -------------------------------------------

    /// <summary>
    /// An `inputMediaPoll` read into heap values. Every ref-struct view is
    /// consumed here so a caller can await freely afterwards.
    /// </summary>
    public readonly record struct PollInput(long RequestedId, bool Closed,
        bool PublicVoters, bool MultipleChoice, bool Quiz, byte[] QuestionBytes,
        byte[] AnswersVectorBytes, IReadOnlyList<byte[]> Options, int ClosePeriod,
        int CloseDate, IReadOnlyList<byte[]> CorrectAnswers, byte[]? Solution,
        byte[]? SolutionEntitiesVector)
    {
        /// <summary>
        /// True when this is pinned TDLib's poll-close request rather than a new
        /// poll. `StopPollQuery` (`PollManager.cpp:194-215`) closes a poll by
        /// sending `messages.editMessage` with an `inputMediaPoll` whose inner
        /// poll carries the existing id, `closed`, an empty question and no
        /// answers. Treating that as a replacement would wipe the question and
        /// every option off the message.
        /// </summary>
        public bool IsCloseRequest => Closed && Options.Count == 0;
    }

    /// The persisted definition, read into heap values.
    public readonly record struct PollSnapshot(long PollId, byte[] PollBytes,
        MessageIdentity Identity, bool Closed, bool PublicVoters,
        bool MultipleChoice, bool Quiz, byte[] QuestionBytes,
        byte[] AnswersVectorBytes, IReadOnlyList<byte[]> Options, int ClosePeriod,
        int CloseDate, IReadOnlyList<byte[]> CorrectAnswers, byte[]? Solution,
        byte[]? SolutionEntitiesVector, int Date);

    /// One ballot, read into heap values.
    public readonly record struct VoteSnapshot(long UserId, TLPeer.PeerType PeerType,
        long PeerId, IReadOnlyList<byte[]> Options, int Date);

    // ---- reading a request ----------------------------------------------

    /// <summary>
    /// Reads an `inputMediaPoll` payload. Returns false for any other media, so
    /// callers keep their existing resolution path.
    /// </summary>
    public static bool TryReadInputPoll(Span<byte> inputMediaBytes,
        out PollInput input)
    {
        input = default;
        var view = (InputMediaView)inputMediaBytes;
        if (!view.Is(out InputMediaPoll media))
        {
            return false;
        }

        var poll = (Poll)media.Poll;
        input = new PollInput(poll.Id, poll.Closed, poll.PublicVoters,
            poll.MultipleChoice, poll.Quiz, poll.Question.ToArray(),
            poll.Answers.ToReadOnlySpan().ToArray(), ReadAnswerOptions(poll.Answers),
            poll.ClosePeriod, poll.CloseDate, ReadBytesVector(media.CorrectAnswers),
            media.Flags[1] ? media.Solution.ToArray() : null,
            media.Flags[1] ? media.SolutionEntities.ToReadOnlySpan().ToArray() : null);
        return true;
    }

    /// <summary>
    /// Rejects a poll definition the protocol does not permit. Pinned TDLib
    /// contains none of these error strings, because it refuses the same inputs
    /// locally before sending; they follow the documented API names so a client
    /// that does send one is answered truthfully rather than with a shrug.
    /// </summary>
    public static ErrorMessage? ValidateDefinition(PollInput input)
    {
        if (input.QuestionBytes.Length == 0)
        {
            return new ErrorMessage(400, "POLL_QUESTION_INVALID");
        }
        if (input.Options.Count < MinAnswers || input.Options.Count > MaxAnswers)
        {
            return new ErrorMessage(400, "POLL_ANSWERS_INVALID");
        }
        var seen = new HashSet<string>();
        foreach (byte[] option in input.Options)
        {
            if (option.Length == 0)
            {
                return new ErrorMessage(400, "POLL_OPTION_INVALID");
            }
            if (!seen.Add(Convert.ToHexString(option)))
            {
                return new ErrorMessage(400, "POLL_OPTION_DUPLICATE");
            }
        }

        if (!input.Quiz)
        {
            return null;
        }
        if (input.MultipleChoice)
        {
            return new ErrorMessage(400, "QUIZ_MULTIPLE_INVALID");
        }
        if (input.CorrectAnswers.Count == 0)
        {
            return new ErrorMessage(400, "QUIZ_CORRECT_ANSWERS_EMPTY");
        }
        if (input.CorrectAnswers.Count > 1)
        {
            return new ErrorMessage(400, "QUIZ_CORRECT_ANSWERS_TOO_MUCH");
        }
        return seen.Contains(Convert.ToHexString(input.CorrectAnswers[0]))
            ? null
            : new ErrorMessage(400, "QUIZ_CORRECT_ANSWER_INVALID");
    }

    // ---- creating -------------------------------------------------------

    /// <summary>
    /// Allocates the poll id and builds the `messageMediaPoll` a freshly sent
    /// message carries. The definition itself is stored by
    /// <see cref="Persist"/> once the send has produced a message identity.
    /// </summary>
    public async Task<PollSnapshot> CreateAsync(PollInput input, int date)
    {
        long pollId = await _ids.NextPollIdAsync();
        int closeDate = input.CloseDate;
        int closePeriod = input.ClosePeriod;
        if (closePeriod > 0 && closeDate == 0)
        {
            closeDate = date + closePeriod;
        }
        else if (closeDate > 0 && closePeriod == 0)
        {
            closePeriod = Math.Max(0, closeDate - date);
        }

        return new PollSnapshot(pollId, Array.Empty<byte>(),
            default, false, input.PublicVoters, input.MultipleChoice, input.Quiz,
            input.QuestionBytes, input.AnswersVectorBytes, input.Options,
            closePeriod, closeDate, input.CorrectAnswers, input.Solution,
            input.SolutionEntitiesVector, date);
    }

    /// <summary>
    /// Stores the definition against the message identity that now carries it.
    /// A common-box poll is keyed by its logical id, so every per-owner copy of
    /// the message shares one poll and one set of ballots.
    /// </summary>
    public bool Persist(PollSnapshot poll, MessageIdentity identity)
    {
        using TLPoll value = BuildPoll(poll, closed: poll.Closed);
        var builder = PollState.Builder()
            .PollId(poll.PollId)
            .BoxType(identity.BoxType)
            .BoxId(identity.BoxId)
            .MessageId(identity.MessageId)
            .Poll(value.AsSpan())
            .Date(poll.Date);
        if (poll.CorrectAnswers.Count > 0)
        {
            builder = builder.CorrectAnswers(BuildBytesVector(poll.CorrectAnswers));
        }
        if (poll.Solution != null)
        {
            builder = builder.Solution(poll.Solution)
                .SolutionEntities(poll.SolutionEntitiesVector == null
                    ? new Vector()
                    : new Vector(poll.SolutionEntitiesVector.AsSpan()));
        }
        using TLPollState row = builder.Build();
        return _pollsRepository.PutPoll(row);
    }

    // ---- reading state --------------------------------------------------

    public async ValueTask<PollSnapshot?> GetAsync(MessageIdentity identity)
    {
        using TLPollState? row = await _pollsRepository
            .GetPollAsync(identity);
        return row == null ? null : ReadSnapshot(row.Value);
    }

    public async ValueTask<IReadOnlyList<VoteSnapshot>> GetVotesAsync(long pollId)
    {
        IReadOnlyCollection<TLPollVote> rows = await _pollsRepository
            .GetVotesAsync(pollId);
        var votes = new List<VoteSnapshot>(rows.Count);
        foreach (TLPollVote row in rows)
        {
            using TLPollVote vote = row;
            var body = vote.AsPollVote();
            votes.Add(new VoteSnapshot(body.UserId, (TLPeer.PeerType)body.PeerType,
                body.PeerId, ReadBytesVector(body.Options), body.Date));
        }
        // A stable order is what makes the opaque getPollVotes offset resumable.
        return votes.OrderBy(x => x.Date).ThenBy(x => x.UserId).ToArray();
    }

    /// <summary>
    /// A poll stops accepting votes either because it was explicitly closed or
    /// because its close date has passed. Deriving the timeout keeps one rule
    /// answering both, so no background sweep has to rewrite a row to make an
    /// expired poll behave as closed.
    /// </summary>
    public static bool IsClosed(PollSnapshot poll, int now) =>
        poll.Closed || (poll.CloseDate > 0 && poll.CloseDate <= now);

    // ---- voting ---------------------------------------------------------

    public readonly record struct VoteOutcome(ErrorMessage? Error,
        IReadOnlyList<VoteSnapshot> Votes)
    {
        public static VoteOutcome Fail(int code, string message) =>
            new(new ErrorMessage(code, message), Array.Empty<VoteSnapshot>());
    }

    /// <summary>
    /// Replaces the caller's ballot. An empty option list retracts it, which is
    /// how a client cancels a vote in a regular poll. The unit of work is
    /// committed by the caller.
    /// </summary>
    public async Task<VoteOutcome> VoteAsync(PollSnapshot poll, long voterUserId,
        TLPeer.PeerType voterPeerType, long voterPeerId,
        IReadOnlyList<byte[]> options, int now)
    {
        if (IsClosed(poll, now))
        {
            return VoteOutcome.Fail(400, "MESSAGE_POLL_CLOSED");
        }
        if (options.Count > 1 && !poll.MultipleChoice)
        {
            return VoteOutcome.Fail(400, "OPTIONS_TOO_MUCH");
        }

        var known = poll.Options.Select(Convert.ToHexString).ToHashSet();
        var chosen = new HashSet<string>();
        foreach (byte[] option in options)
        {
            string key = Convert.ToHexString(option);
            if (!known.Contains(key) || !chosen.Add(key))
            {
                return VoteOutcome.Fail(400, "POLL_OPTION_INVALID");
            }
        }

        using (TLPollVote? existing = await _pollsRepository
                   .GetVoteAsync(poll.PollId, voterUserId))
        {
            // A quiz answer is final: letting a client retry would turn a graded
            // question into an oracle.
            if (existing != null && poll.Quiz)
            {
                return VoteOutcome.Fail(400, "REVOTE_NOT_ALLOWED");
            }
        }

        if (options.Count == 0)
        {
            _pollsRepository.DeleteVote(poll.PollId, voterUserId);
        }
        else
        {
            using TLPollVote row = PollVote.Builder()
                .PollId(poll.PollId)
                .UserId(voterUserId)
                .PeerType((int)voterPeerType)
                .PeerId(voterPeerId)
                .Options(BuildBytesVector(options))
                .Date(now)
                .Build();
            _pollsRepository.PutVote(row);
        }

        // The repository write is not visible to a subsequent read until the unit
        // of work commits, so the caller's own ballot is folded in here.
        var votes = (await GetVotesAsync(poll.PollId))
            .Where(x => x.UserId != voterUserId)
            .ToList();
        if (options.Count > 0)
        {
            votes.Add(new VoteSnapshot(voterUserId, voterPeerType, voterPeerId,
                options, now));
        }
        return new VoteOutcome(null,
            votes.OrderBy(x => x.Date).ThenBy(x => x.UserId).ToArray());
    }

    /// <summary>
    /// Closes an existing poll in place, keeping its question, options and every
    /// ballot already cast.
    /// </summary>
    public bool Close(PollSnapshot poll, MessageIdentity identity)
    {
        PollSnapshot closed = poll with { Closed = true };
        return Persist(closed, identity);
    }

    // ---- building the wire values ---------------------------------------

    /// <summary>
    /// The `poll` constructor as clients see it. `close_period` and `close_date`
    /// are emitted together or not at all: pinned TDLib discards BOTH when either
    /// is missing (`PollManager.cpp:1698-1703`), so a lone close date silently
    /// leaves the client with a poll that never expires.
    /// </summary>
    public static TLPoll BuildPoll(PollSnapshot poll, bool closed)
    {
        var builder = Poll.Builder()
            .Id(poll.PollId)
            .Closed(closed)
            .PublicVoters(poll.PublicVoters)
            .MultipleChoice(poll.MultipleChoice)
            .Quiz(poll.Quiz)
            .Question(poll.QuestionBytes)
            .Answers(new Vector(poll.AnswersVectorBytes.AsSpan()));
        if (!closed && poll.CloseDate > 0 && poll.ClosePeriod > 0)
        {
            builder = builder.ClosePeriod(poll.ClosePeriod).CloseDate(poll.CloseDate);
        }
        return builder.Build();
    }

    /// <summary>
    /// The `pollResults` one viewer is entitled to see. Before that viewer has
    /// voted, and while the poll is open, the per-option tallies stay hidden
    /// behind `min` exactly as Telegram hides them; once they have voted, or once
    /// the poll closes, the full breakdown plus their own `chosen` flags and the
    /// quiz answer are revealed.
    /// </summary>
    public static TLPollResults BuildResults(PollSnapshot poll,
        IReadOnlyList<VoteSnapshot> votes, long viewerUserId, int now)
    {
        bool closed = IsClosed(poll, now);
        VoteSnapshot[] own = votes.Where(x => x.UserId == viewerUserId).ToArray();
        bool revealed = own.Length > 0 || closed;
        var chosen = own.Length == 0
            ? new HashSet<string>()
            : own[0].Options.Select(Convert.ToHexString).ToHashSet();
        var correct = poll.CorrectAnswers.Select(Convert.ToHexString).ToHashSet();

        var builder = PollResults.Builder().TotalVoters(votes.Count);
        if (!revealed)
        {
            builder = builder.Min(true);
        }
        else
        {
            var tallies = new Vector();
            foreach (byte[] option in poll.Options)
            {
                string key = Convert.ToHexString(option);
                int voters = votes.Count(vote => vote.Options.Any(
                    picked => Convert.ToHexString(picked) == key));
                using TLPollAnswerVoters answer = PollAnswerVoters.Builder()
                    .Chosen(chosen.Contains(key))
                    .Correct(poll.Quiz && correct.Contains(key))
                    .Option(option)
                    .Voters(voters)
                    .Build();
                tallies.AppendTLObject(answer.AsSpan());
            }
            builder = builder.Results(tallies);
            if (poll.Quiz && poll.Solution != null)
            {
                builder = builder.Solution(poll.Solution)
                    .SolutionEntities(poll.SolutionEntitiesVector == null
                        ? new Vector()
                        : new Vector(poll.SolutionEntitiesVector.AsSpan()));
            }
        }

        if (poll.PublicVoters && votes.Count > 0)
        {
            var recent = new Vector();
            foreach (VoteSnapshot vote in votes.OrderByDescending(x => x.Date)
                         .ThenByDescending(x => x.UserId).Take(RecentVoterLimit))
            {
                using TLPeer peer = PeerResolver.BuildPeer(vote.PeerType, vote.PeerId);
                recent.AppendTLObject(peer.AsSpan());
            }
            builder = builder.RecentVoters(recent);
        }
        return builder.Build();
    }

    /// The `messageMediaPoll` a stored row or an update carries.
    public static byte[] BuildMedia(PollSnapshot poll,
        IReadOnlyList<VoteSnapshot> votes, long viewerUserId, int now)
    {
        using TLPoll value = BuildPoll(poll, IsClosed(poll, now));
        using TLPollResults results = BuildResults(poll, votes, viewerUserId, now);
        using TLMessageMedia media = MessageMediaPoll.Builder()
            .Poll(value.AsSpan())
            .Results(results.AsSpan())
            .Build();
        return media.AsSpan().ToArray();
    }

    /// <summary>
    /// The `updateMessagePoll` a single viewer receives. It carries no peer or
    /// message id, so this per-viewer `results` value is the only way that viewer
    /// learns which options they themselves chose.
    /// </summary>
    public static TLUpdate BuildUpdate(PollSnapshot poll,
        IReadOnlyList<VoteSnapshot> votes, long viewerUserId, int now)
    {
        using TLPoll value = BuildPoll(poll, IsClosed(poll, now));
        using TLPollResults results = BuildResults(poll, votes, viewerUserId, now);
        return UpdateMessagePoll.Builder()
            .PollId(poll.PollId)
            .Poll(value.AsSpan())
            .Results(results.AsSpan())
            .Build();
    }

    public int UnixNow() =>
        checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());

    // ---- plumbing -------------------------------------------------------

    private static PollSnapshot ReadSnapshot(TLPollState row)
    {
        var state = row.AsPollState();
        var poll = (Poll)state.Poll;
        return new PollSnapshot(state.PollId, state.Poll.ToArray(),
            new MessageIdentity(state.BoxType, state.BoxId, state.MessageId),
            poll.Closed, poll.PublicVoters, poll.MultipleChoice, poll.Quiz,
            poll.Question.ToArray(), poll.Answers.ToReadOnlySpan().ToArray(),
            ReadAnswerOptions(poll.Answers), poll.ClosePeriod, poll.CloseDate,
            ReadBytesVector(state.CorrectAnswers),
            state.Flags[1] ? state.Solution.ToArray() : null,
            state.Flags[1] ? state.SolutionEntities.ToReadOnlySpan().ToArray() : null,
            state.Date);
    }

    private static IReadOnlyList<byte[]> ReadAnswerOptions(Vector answers)
    {
        var options = new List<byte[]>(answers.Count);
        int count = answers.Count;
        for (int i = 0; i < count; i++)
        {
            options.Add(new PollAnswer(answers.ReadTLObject()).Option.ToArray());
        }
        return options;
    }

    private static IReadOnlyList<byte[]> ReadBytesVector(VectorOfString source)
    {
        var values = new List<byte[]>(source.Count);
        int count = source.Count;
        for (int i = 0; i < count; i++)
        {
            values.Add(source.ReadTLBytes().ToArray());
        }
        return values;
    }

    private static VectorOfString BuildBytesVector(IReadOnlyList<byte[]> values)
    {
        var vector = new VectorOfString();
        foreach (byte[] value in values)
        {
            vector.AppendTLBytes(value);
        }
        return vector;
    }
}
