// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetOnlinesHandler
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IUserStatusRepository _userStatusRepository;

    private readonly IUnitOfWork _unitOfWork;

    public GetOnlinesHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IUserStatusRepository userStatusRepository) {
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _authorizationRepository = authorizationRepository;
        _userStatusRepository = userStatusRepository;
        _unitOfWork = unitOfWork;
    }

    [TLFunction(Constructors.baseLayer_GetOnlines)]
    public async ValueTask<TLChatOnlines> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return (TLChatOnlines)RpcErrorGenerator.GenerateError(401, "AUTH_KEY_INVALID"u8);
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        var request = (GetOnlines)q;
        long channelId = PeerResolver.ResolveInputPeerChannelId(request.Get_PeerView());
        (TLPeer.PeerType peerType, long peerId) = PeerResolver.ResolveHistoryPeer(
            request.Get_PeerView(), userId);

        long chatId;
        if (channelId > 0)
        {
            string? accessError = await ChannelAccess.ValidateReadAsync(_chatRepository, _chatParticipantsRepository, channelId, userId);
            if (accessError != null)
            {
                return (TLChatOnlines)RpcErrorGenerator.GenerateError(400,
                    System.Text.Encoding.UTF8.GetBytes(accessError));
            }
            chatId = channelId;
        }
        else if (peerType == TLPeer.PeerType.PeerChat && peerId > 0)
        {
            chatId = peerId;
        }
        else
        {
            return (TLChatOnlines)RpcErrorGenerator.GenerateError(400, "PEER_ID_INVALID"u8);
        }

        int onlines = await CountOnlineAsync(chatId);
        return ChatOnlines.Builder().Onlines(onlines).Build();
    }

    private async ValueTask<int> CountOnlineAsync(long chatId)
    {
        IReadOnlyCollection<TLChatParticipantInfo> participants = await _chatParticipantsRepository.GetParticipantsAsync(chatId);

        var counted = new HashSet<long>();
        foreach (TLChatParticipantInfo participant in participants)
        {
            using (participant)
            {
                var info = participant.AsChatParticipantInfo();
                int role = info.Role;
                if (role is (int)ChatParticipantRole.Banned
                    or (int)ChatParticipantRole.Left)
                {
                    continue;
                }
                counted.Add(info.UserId);
            }
        }

        int onlines = 0;
        foreach (long participantId in counted)
        {
            using TLUserStatus status = await _userStatusRepository
                .GetUserStatusAsync(participantId);
            if (status.Type == TLUserStatus.UserStatusType.UserStatusOnline)
            {
                onlines++;
            }
        }
        return onlines;
    }
}
