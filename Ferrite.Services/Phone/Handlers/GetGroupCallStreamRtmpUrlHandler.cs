// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;

namespace Ferrite.Services.Phone.Handlers;

public sealed class GetGroupCallStreamRtmpUrlHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IGroupCallsRepository _groupCallsRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IGroupCallBroadcastPlane _broadcast;

    public GetGroupCallStreamRtmpUrlHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IGroupCallsRepository groupCallsRepository,
        IGroupCallBroadcastPlane broadcast)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _groupCallsRepository = groupCallsRepository;

        _unitOfWork = unitOfWork;
        _broadcast = broadcast;
    }

    [TLFunction(Constructors.baseLayer_GetGroupCallStreamRtmpUrl)]
    public async ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetGroupCallStreamRtmpUrl)q;
        bool peerResolved = GroupCallAccess.TryResolveCallPeer(
            request.Get_PeerView(), out GroupCallPeerRef peer);
        bool revoke = request.Revoke;
        if (!peerResolved)
        {
            return Error(GroupCallErrors.PeerIdInvalid);
        }

        GroupCallPeerAccess access = await GroupCallAccess.AuthorizeAsync(
            _authorizationRepository, _chatRepository,
            _chatParticipantsRepository, authKeyId, peer,
            GroupCallAccessLevel.Manage);
        if (access.Error != null)
        {
            return Error(access.Error);
        }
        using var call = await _groupCallsRepository
            .GetActiveCallByPeerAsync((int)peer.Type, peer.Id);
        if (call == null)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }
        long callId = call.Value.AsGroupCallState().Id;
        bool rtmpStream = call.Value.AsGroupCallState().RtmpStream;

        if (!rtmpStream)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }

        try
        {
            await _broadcast.CreateStreamAsync(callId, rtmpStream);
            GroupCallBroadcastCredentials credentials = await _broadcast
                .GetCredentialsAsync(callId, revoke);
            var result = GroupCallStreamRtmpUrl.Builder()
                .Url(Encoding.UTF8.GetBytes(credentials.Url))
                .Key(Encoding.UTF8.GetBytes(credentials.Key))
                .Build();
            return result.TLBytes!.Value;
        }
        catch (GroupCallBroadcastException)
        {
            return Error(GroupCallErrors.MediaUnavailable);
        }
    }

    private static TLBytes Error(string message) =>
        RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
