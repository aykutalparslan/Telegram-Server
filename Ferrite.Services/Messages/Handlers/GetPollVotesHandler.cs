// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

/// <summary>
/// Pages the individual voters of a non-anonymous poll. Pinned TDLib refuses an
/// anonymous poll locally (<c>PollManager.cpp:1126-1127</c>) and never sends the
/// query, so the server-side refusal here is the backstop for any other client
/// rather than a path the pinned client exercises.
///
/// The opaque <c>next_offset</c> is the last returned ballot's position in the
/// same (date, voter) order the store returns, which is what makes a resumed page
/// continue exactly where the previous one stopped even as new votes arrive.
/// </summary>
public sealed class GetPollVotesHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    /// Telegram's documented per-page ceiling for voter lists.
    private const int MaxLimit = 100;

    private readonly IUnitOfWork _unitOfWork;
    private readonly MessageLocator _locator;
    private readonly UpdateFanout _fanout;
    private readonly PollStore _polls;

    public GetPollVotesHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, MessageLocator locator,
        UpdateFanout fanout, PollStore polls)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _locator = locator;
        _fanout = fanout;
        _polls = polls;
    }

    [TLFunction(Constructors.baseLayer_GetPollVotes)]
    public async Task<TLVotesList> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return Error(400, "AUTH_KEY_INVALID");
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        var request = (GetPollVotes)q;
        if (!PeerResolver.TryResolveInputPeerDialogKey(request.Get_PeerView(),
                userId, out DialogPeerKey peer))
        {
            return Error(400, "PEER_ID_INVALID");
        }
        int messageId = request.Id;
        byte[]? option = request.Flags[0] ? request.Option.ToArray() : null;
        string offset = request.Flags[1]
            ? Encoding.UTF8.GetString(request.Offset)
            : string.Empty;
        int limit = request.Limit;

        if (limit <= 0 || limit > MaxLimit)
        {
            return Error(400, "LIMIT_INVALID");
        }

        MessageIdentity? identity = await _locator.ResolveIdentityAsync(userId,
            peer.Type, peer.Id, messageId);
        if (identity == null)
        {
            return Error(400, "MESSAGE_ID_INVALID");
        }

        PollStore.PollSnapshot? stored = await _polls.GetAsync(identity.Value);
        if (stored == null)
        {
            return Error(400, "MESSAGE_POLL_MISSING");
        }
        PollStore.PollSnapshot poll = stored.Value;
        if (!poll.PublicVoters)
        {
            return Error(400, "POLL_VOTERS_FORBIDDEN");
        }
        if (option != null &&
            !poll.Options.Any(known => known.SequenceEqual(option)))
        {
            return Error(400, "POLL_OPTION_INVALID");
        }

        IReadOnlyList<PollStore.VoteSnapshot> votes =
            await _polls.GetVotesAsync(poll.PollId);
        List<PollStore.VoteSnapshot> matching = option == null
            ? votes.ToList()
            : votes.Where(vote => vote.Options.Any(
                picked => picked.SequenceEqual(option))).ToList();

        int start = ResolveOffset(matching, offset);
        if (start < 0)
        {
            return Error(400, "OFFSET_INVALID");
        }
        List<PollStore.VoteSnapshot> page = matching.Skip(start).Take(limit).ToList();
        string? nextOffset = start + page.Count < matching.Count && page.Count > 0
            ? EncodeOffset(page[^1])
            : null;

        List<byte[]> chats = peer.Type == TLPeer.PeerType.PeerUser
            ? new List<byte[]>()
            : await _fanout.GetChatBytesForViewerAsync(userId, new[] { peer.Id });
        return BuildResult(matching.Count, page, option, chats, nextOffset);
    }

    /// <summary>
    /// The index the next page starts at. An empty offset starts from the
    /// beginning; an offset naming a ballot that has since been retracted is
    /// rejected rather than silently restarting the list from the top.
    /// </summary>
    private static int ResolveOffset(IReadOnlyList<PollStore.VoteSnapshot> votes,
        string offset)
    {
        if (offset.Length == 0)
        {
            return 0;
        }
        for (int i = 0; i < votes.Count; i++)
        {
            if (EncodeOffset(votes[i]) == offset)
            {
                return i + 1;
            }
        }
        return -1;
    }

    private static string EncodeOffset(PollStore.VoteSnapshot vote) =>
        vote.Date.ToString() + "_" + vote.UserId.ToString();

    // Synchronous so the ref-struct vectors never cross an await.
    private TLVotesList BuildResult(int count,
        IReadOnlyList<PollStore.VoteSnapshot> page, byte[]? requestedOption,
        IReadOnlyList<byte[]> chatBytes, string? nextOffset)
    {
        var votes = new Vector();
        foreach (PollStore.VoteSnapshot vote in page)
        {
            using TLPeer peer = PeerResolver.BuildPeer(vote.PeerType, vote.PeerId);
            using TLMessagePeerVote element = BuildVote(vote, peer, requestedOption);
            votes.AppendTLObject(element.AsSpan());
        }

        var chats = new Vector();
        foreach (byte[] bytes in chatBytes)
        {
            chats.AppendTLObject(bytes);
        }
        var users = new Vector();
        _fanout.AppendUsers(ref users, page
            .Where(vote => vote.PeerType == TLPeer.PeerType.PeerUser)
            .Select(vote => vote.PeerId)
            .Distinct());

        var builder = VotesList.Builder()
            .Count(count)
            .Votes(votes)
            .Chats(chats)
            .Users(users);
        if (nextOffset != null)
        {
            builder = builder.NextOffset(Encoding.UTF8.GetBytes(nextOffset));
        }
        return builder.Build();
    }

    /// <summary>
    /// When the request named an option, the reduced constructor is correct: the
    /// option is already known to the caller and repeating it in every row would
    /// only restate the filter.
    /// </summary>
    private static TLMessagePeerVote BuildVote(PollStore.VoteSnapshot vote,
        TLPeer peer, byte[]? requestedOption)
    {
        if (requestedOption != null)
        {
            return MessagePeerVoteInputOption.Builder()
                .Peer(peer.AsSpan())
                .Date(vote.Date)
                .Build();
        }
        if (vote.Options.Count == 1)
        {
            return MessagePeerVote.Builder()
                .Peer(peer.AsSpan())
                .Option(vote.Options[0])
                .Date(vote.Date)
                .Build();
        }

        var options = new VectorOfString();
        foreach (byte[] picked in vote.Options)
        {
            options.AppendTLBytes(picked);
        }
        return MessagePeerVoteMultiple.Builder()
            .Peer(peer.AsSpan())
            .Options(options)
            .Date(vote.Date)
            .Build();
    }

    private static TLVotesList Error(int code, string message) =>
        (TLVotesList)RpcErrorGenerator.GenerateError(code,
            Encoding.UTF8.GetBytes(message));
}
