// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Phone.Handlers;

/// <summary>
/// phone.saveDefaultGroupCallJoinAs. The current supported join-as set contains
/// only self, and the choice is persisted per account and hosting peer so the
/// next chatFull/channelFull exposes the same default.
/// </summary>
public sealed class SaveDefaultGroupCallJoinAsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IGroupCallsRepository _groupCallsRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMTProtoTime _time;

    public SaveDefaultGroupCallJoinAsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IGroupCallsRepository groupCallsRepository, IMTProtoTime time)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _groupCallsRepository = groupCallsRepository;

        _unitOfWork = unitOfWork;
        _time = time;
    }

    [TLFunction(Constructors.baseLayer_SaveDefaultGroupCallJoinAs)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = (SaveDefaultGroupCallJoinAs)q;
        if (!GroupCallAccess.TryResolveCallPeer(request.Get_PeerView(), out GroupCallPeerRef peer))
        {
            return Error(GroupCallErrors.PeerIdInvalid);
        }
        JoinAsCandidate joinAs = ReadJoinAs(request.Get_JoinAsView());

        if (!joinAs.Valid)
        {
            return Error(GroupCallErrors.JoinAsPeerInvalid);
        }

        GroupCallPeerAccess access = await GroupCallAccess.AuthorizeAsync(_authorizationRepository, _chatRepository, _chatParticipantsRepository, authKeyId, peer, GroupCallAccessLevel.Participate);
        if (access.Error != null)
        {
            return Error(access.Error);
        }
        if (!joinAs.IsSelf && joinAs.UserId != access.CurrentUserId)
        {
            return Error(GroupCallErrors.JoinAsPeerInvalid);
        }

        using TLDto.TLGroupCallDefaultJoinAs row = TLDto.GroupCallDefaultJoinAs.Builder()
            .UserId(access.CurrentUserId)
            .PeerType((int)peer.Type)
            .PeerId(peer.Id)
            .JoinAsPeerType((int)TLPeer.PeerType.PeerUser)
            .JoinAsPeerId(access.CurrentUserId)
            .Date(checked((int)_time.GetUnixTimeInSeconds()))
            .Build();
        await _groupCallsRepository.SaveDefaultJoinAsAsync(row);
        await _unitOfWork.SaveAsync();
        return BoolTrue.Builder().Build();
    }

    private readonly record struct JoinAsCandidate(bool Valid, bool IsSelf, long UserId);

    private static JoinAsCandidate ReadJoinAs(InputPeerView peer)
    {
        if (peer.Is(out InputPeerSelf _))
        {
            return new JoinAsCandidate(true, true, 0);
        }
        if (peer.Is(out InputPeerUser user) && user.UserId > 0)
        {
            return new JoinAsCandidate(true, false, user.UserId);
        }

        return default;
    }

    private static TLBool Error(string message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400, Encoding.UTF8.GetBytes(message));
}
