// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetPollResultsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly MessageLocator _locator;
    private readonly UpdateFanout _fanout;
    private readonly PollStore _polls;

    public GetPollResultsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, MessageLocator locator,
        UpdateFanout fanout, PollStore polls)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _locator = locator;
        _fanout = fanout;
        _polls = polls;
    }

    [TLFunction(Constructors.baseLayer_GetPollResults)]
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

        var request = (GetPollResults)q;
        if (!PeerResolver.TryResolveInputPeerDialogKey(request.Get_PeerView(),
                userId, out DialogPeerKey peer))
        {
            return Error(400, "PEER_ID_INVALID");
        }
        int messageId = request.MsgId;

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
        IReadOnlyList<PollStore.VoteSnapshot> votes =
            await _polls.GetVotesAsync(poll.Value.PollId);
        byte[] updateBytes;
        using (TLUpdate update = PollStore.BuildUpdate(poll.Value, votes, userId, now))
        {
            updateBytes = update.AsSpan().ToArray();
        }

        var userIds = new List<long> { userId };
        if (peer.Type == TLPeer.PeerType.PeerUser)
        {
            userIds.Add(peer.Id);
        }
        List<byte[]> chats = peer.Type == TLPeer.PeerType.PeerUser
            ? new List<byte[]>()
            : await _fanout.GetChatBytesForViewerAsync(userId, new[] { peer.Id });
        return _fanout.BuildUpdates(userId, new[] { updateBytes }, userIds, chats, now, 0);
    }

    private static TLUpdates Error(int code, string message) =>
        (TLUpdates)RpcErrorGenerator.GenerateError(code,
            Encoding.UTF8.GetBytes(message));
}
