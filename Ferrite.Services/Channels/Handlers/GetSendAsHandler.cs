// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.Channels;

public sealed class GetSendAsHandler
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public GetSendAsHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IUserRepository userRepository, TimeProvider timeProvider)
    {
        _chatParticipantsRepository = chatParticipantsRepository;

        _authorizationRepository = authorizationRepository;
        _chatRepository = chatRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    [TLFunction(Constructors.layer135_ChannelsGetSendAs)]
    public async ValueTask<TLSendAsPeers> HandleLayer135(long authKeyId,
        TLBytes q)
    {
        using var current = ToCurrentGetSendAsRequest(q);
        return await Handle(authKeyId, current);
    }

    private static TLBytes ToCurrentGetSendAsRequest(TLBytes q)
    {
        var sent = new TL.layer135.channels.ChannelsGetSendAs(q.AsSpan());
        using var current = GetSendAs.Builder()
            .Peer(sent.Peer)
            .Build();
        return current.TLBytes!.Value;
    }

    [TLFunction(Constructors.baseLayer_GetSendAs)]
    public async ValueTask<TLSendAsPeers> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null)
            {
                return Error("AUTH_KEY_INVALID");
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        var request = (GetSendAs)q;
        DialogPeerKey? destination = PeerResolver.ResolveOptionalDialogPeer(
            request.Get_PeerView(), userId);
        if (destination == null ||
            destination.Value.Type != TLPeer.PeerType.PeerChannel ||
            !await SendAsResolver.CanAddressAsync(_userRepository, _chatRepository, _chatParticipantsRepository, _timeProvider, userId, destination.Value))
        {
            return Error("PEER_ID_INVALID");
        }

        using TLUser? self = _userRepository.GetUser(userId);
        if (self == null)
        {
            return Error("AUTH_KEY_INVALID");
        }

        List<long> candidates = await SendAsResolver
            .GetOwnedSenderChannelIdsAsync(_chatParticipantsRepository, userId);

        var senders = new List<(long Id, TLChat Chat)>();
        try
        {
            foreach (long senderChannelId in candidates)
            {
                TLChat? sender = await _chatRepository
                    .GetChatAsync(senderChannelId);
                if (sender == null)
                {
                    continue;
                }
                if (sender.Value.Type != TLChat.ChatType.Channel)
                {
                    sender.Value.Dispose();
                    continue;
                }
                senders.Add((senderChannelId, sender.Value));
            }

            return BuildSendAsPeers(userId, self.Value, senders);
        }
        finally
        {
            foreach ((long _, TLChat sender) in senders)
            {
                sender.Dispose();
            }
        }
    }

    private static TLSendAsPeers BuildSendAsPeers(long userId, TLUser self,
        IReadOnlyList<(long Id, TLChat Chat)> senders)
    {
        var peers = new Vector();
        var chats = new Vector();
        var users = new Vector();
        users.AppendTLObject(self.AsSpan());
        AppendSender(ref peers, TLPeer.PeerType.PeerUser, userId);
        foreach ((long senderChannelId, TLChat sender) in senders)
        {
            AppendSender(ref peers, TLPeer.PeerType.PeerChannel, senderChannelId);
            chats.AppendTLObject(sender.AsSpan());
        }

        return SendAsPeers.Builder()
            .Peers(peers)
            .Chats(chats)
            .Users(users)
            .Build();
    }

    private static void AppendSender(ref Vector peers, TLPeer.PeerType type,
        long id)
    {
        using TLPeer peer = PeerResolver.BuildPeer(type, id);
        using TLSendAsPeer sender = SendAsPeer.Builder()
            .Peer(peer.AsSpan())
            .Build();
        peers.AppendTLObject(sender.AsSpan());
    }

    private static TLSendAsPeers Error(string message) =>
        (TLSendAsPeers)RpcErrorGenerator.GenerateError(400,
            System.Text.Encoding.UTF8.GetBytes(message));
}
