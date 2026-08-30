// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;

namespace Ferrite.Services.Phone.Handlers;

public sealed class GetGroupCallJoinAsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly UserSerializer _userSerializer;

    private readonly IUnitOfWork _unitOfWork;

    public GetGroupCallJoinAsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, UserSerializer userSerializer)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _userSerializer = userSerializer;

        _unitOfWork = unitOfWork;
    }

    [TLFunction(Constructors.baseLayer_GetGroupCallJoinAs)]
    public async ValueTask<TLJoinAsPeers> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetGroupCallJoinAs)q;
        if (!GroupCallAccess.TryResolveCallPeer(request.Get_PeerView(), out GroupCallPeerRef peer))
        {
            return Error(GroupCallErrors.PeerIdInvalid);
        }

        GroupCallPeerAccess access = await GroupCallAccess.AuthorizeAsync(_authorizationRepository, _chatRepository, _chatParticipantsRepository, authKeyId, peer, GroupCallAccessLevel.Participate);
        if (access.Error != null)
        {
            return Error(access.Error);
        }

        using TLUser? user = _userSerializer.Get(access.CurrentUserId, access.CurrentUserId);
        if (user == null || user.Value.Type != TLUser.UserType.User)
        {
            return Error(GroupCallErrors.PeerIdInvalid);
        }

        var peers = new Vector();
        using (TLPeer self = PeerUser.Builder().UserId(access.CurrentUserId).Build())
        {
            peers.AppendTLObject(self.AsSpan());
        }

        var chats = new Vector();
        var users = new Vector();
        users.AppendTLObject(user.Value.AsSpan());
        return JoinAsPeers.Builder()
            .Peers(peers)
            .Chats(chats)
            .Users(users)
            .Build();
    }

    private static TLJoinAsPeers Error(string message) =>
        (TLJoinAsPeers)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
