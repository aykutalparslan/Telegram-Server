// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class SendVoteHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IUpdatesService _updates;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly MessageLocator _locator;
    private readonly UpdateFanout _fanout;
    private readonly PollStore _polls;
    private readonly ILogger _log;

    public SendVoteHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IUpdatesService updates,
        IUpdatesContextFactory updatesContextFactory, MessageLocator locator,
        UpdateFanout fanout, PollStore polls, ILogger log)
    {
        _authorizationRepository = authorizationRepository;
        _chatRepository = chatRepository;

        _unitOfWork = unitOfWork;
        _updates = updates;
        _updatesContextFactory = updatesContextFactory;
        _locator = locator;
        _fanout = fanout;
        _polls = polls;
        _log = log;
    }

    [TLFunction(Constructors.baseLayer_SendVote)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
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

        var request = (SendVote)q;
        if (!PeerResolver.TryResolveInputPeerDialogKey(request.Get_PeerView(),
                userId, out DialogPeerKey peer))
        {
            return Error(400, "PEER_ID_INVALID");
        }
        int messageId = request.MsgId;
        List<byte[]> options = ReadOptions(request.Options);

        MessageIdentity? identity = await _locator.ResolveIdentityAsync(userId,
            peer.Type, peer.Id, messageId);
        if (identity == null)
        {
            return Error(400, "MESSAGE_ID_INVALID");
        }

        PollStore.PollSnapshot? poll = await _polls.GetAsync(identity.Value);
        if (poll == null)
        {
            return Error(400, "MESSAGE_POLL_MISSING");
        }

        int now = _polls.UnixNow();
        PollStore.VoteOutcome outcome = await _polls.VoteAsync(poll.Value, userId,
            TLPeer.PeerType.PeerUser, userId, options, now);
        if (outcome.Error is { } error)
        {
            return Error(error.Code, error.Message);
        }
        if (!await _unitOfWork.SaveAsync())
        {
            return Error(500, "INTERNAL_SERVER_ERROR");
        }

        _log.Debug($"🗳️ SendVote user:{userId} peer:{peer.Type}:{peer.Id} " +
                   $"id:{messageId} poll:{poll.Value.PollId} " +
                   $"options:{options.Count} voters:{outcome.Votes.Count}");
        return peer.Type == TLPeer.PeerType.PeerChannel
            ? await ApplyChannelVoteAsync(authKeyId, userId, peer.Id, messageId,
                poll.Value, outcome.Votes, now)
            : await ApplyCommonVoteAsync(authKeyId, userId, peer, messageId,
                poll.Value, outcome.Votes, now);
    }

    private async Task<TLUpdates> ApplyCommonVoteAsync(long authKeyId, long userId,
        DialogPeerKey peer, int messageId, PollStore.PollSnapshot poll,
        IReadOnlyList<PollStore.VoteSnapshot> votes, int now)
    {
        IReadOnlyList<StoredMessageLocation> updated = await _locator
            .MutateCommonCopiesAsync(userId, messageId, location =>
                RebuildMedia(location.MessageBytes,
                    PollStore.BuildMedia(poll, votes, location.OwnerId, now)));
        if (updated.Count == 0)
        {
            return Error(400, "MESSAGE_ID_INVALID");
        }
        if (!await _unitOfWork.SaveAsync())
        {
            return Error(500, "INTERNAL_SERVER_ERROR");
        }

        byte[]? callerUpdateBytes = null;
        foreach (StoredMessageLocation copy in updated)
        {
            if (copy.OwnerId == userId)
            {
                using TLUpdate own = PollStore.BuildUpdate(poll, votes, userId, now);
                callerUpdateBytes = own.AsSpan().ToArray();
                continue;
            }
            await _updates.EnqueueUpdate(copy.OwnerId,
                PollStore.BuildUpdate(poll, votes, copy.OwnerId, now));
        }
        if (callerUpdateBytes == null)
        {
            return Error(400, "MESSAGE_ID_INVALID");
        }

        var userIds = new List<long> { userId };
        if (peer.Type == TLPeer.PeerType.PeerUser)
        {
            userIds.Add(peer.Id);
        }
        List<byte[]> chats = peer.Type == TLPeer.PeerType.PeerChat
            ? await _fanout.GetChatBytesForViewerAsync(userId, new[] { peer.Id })
            : new List<byte[]>();
        int seq = await _updatesContextFactory.GetUpdatesContext(authKeyId, userId)
            .IncrementSeq();
        return _fanout.BuildUpdates(userId, new[] { callerUpdateBytes }, userIds, chats,
            now, seq);
    }

    private async Task<TLUpdates> ApplyChannelVoteAsync(long authKeyId, long userId,
        long channelId, int messageId, PollStore.PollSnapshot poll,
        IReadOnlyList<PollStore.VoteSnapshot> votes, int now)
    {
        StoredMessageLocation? updated = await _locator.MutateChannelAsync(channelId,
            messageId, location => RebuildMedia(location.MessageBytes,
                PollStore.BuildMedia(poll, votes, 0, now)));
        if (updated == null)
        {
            return Error(400, "MESSAGE_ID_INVALID");
        }
        if (!await _unitOfWork.SaveAsync())
        {
            return Error(500, "INTERNAL_SERVER_ERROR");
        }

        List<long> memberIds = await _fanout.GetOtherActiveChannelMemberIdsAsync(
            channelId, userId);
        foreach (long memberId in memberIds)
        {
            await _updates.EnqueueUpdate(memberId,
                PollStore.BuildUpdate(poll, votes, memberId, now));
        }

        byte[] callerUpdateBytes;
        using (TLUpdate callerUpdate = PollStore.BuildUpdate(poll, votes, userId, now))
        {
            callerUpdateBytes = callerUpdate.AsSpan().ToArray();
        }
        byte[] channelBytes;
        using (TLChat? chat = await _chatRepository.GetChatAsync(channelId))
        {
            if (chat == null)
            {
                return Error(400, "CHANNEL_INVALID");
            }
            channelBytes = chat.Value.AsSpan().ToArray();
        }
        int seq = await _updatesContextFactory.GetUpdatesContext(authKeyId, userId)
            .IncrementSeq();
        return _fanout.BuildUpdates(userId, new[] { callerUpdateBytes }, new[] { userId },
            new[] { channelBytes }, now, seq);
    }

    private static byte[] RebuildMedia(byte[] storedBytes, byte[] mediaBytes)
    {
        using var stored = new TLMessage(storedBytes, 0, storedBytes.Length);
        if (stored.Type != TLMessage.MessageType.Message)
        {
            return storedBytes;
        }
        using TLMessage rebuilt = MessageRows.RebuildMedia(stored.AsMessage(),
            mediaBytes);
        return rebuilt.AsSpan().ToArray();
    }

    private static List<byte[]> ReadOptions(VectorOfString options)
    {
        var values = new List<byte[]>(options.Count);
        int count = options.Count;
        for (int i = 0; i < count; i++)
        {
            values.Add(options.ReadTLBytes().ToArray());
        }
        return values;
    }

    private static TLUpdates Error(int code, string message) =>
        (TLUpdates)RpcErrorGenerator.GenerateError(code,
            Encoding.UTF8.GetBytes(message));
}
