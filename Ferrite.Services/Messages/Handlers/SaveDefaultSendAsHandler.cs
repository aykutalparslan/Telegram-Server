// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class SaveDefaultSendAsHandler
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ChatSettingsStore _settings;
    private readonly TimeProvider _timeProvider;

    public SaveDefaultSendAsHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IUserRepository userRepository, IAuthorizationRepository authorizationRepository,
        ChatSettingsStore settings, TimeProvider timeProvider)
    {
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _userRepository = userRepository;

        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _settings = settings;
        _timeProvider = timeProvider;
    }

    [TLFunction(Constructors.baseLayer_SaveDefaultSendAs)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return Error("AUTH_KEY_INVALID");
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        var request = (SaveDefaultSendAs)q;
        DialogPeerKey? destination = PeerResolver.ResolveOptionalDialogPeer(
            request.Get_PeerView(), userId);
        DialogPeerKey? sendAs = PeerResolver.ResolveOptionalDialogPeer(
            request.Get_SendAsView(), userId);
        if (destination == null ||
            destination.Value.Type != TLPeer.PeerType.PeerChannel ||
            destination.Value.Id <= 0 ||
            !await SendAsResolver.CanAddressAsync(_userRepository, _chatRepository, _chatParticipantsRepository, _timeProvider,
                userId, destination.Value))
        {
            return Error("PEER_ID_INVALID");
        }
        if (sendAs == null || sendAs.Value.Id <= 0)
        {
            return Error("SEND_AS_PEER_INVALID");
        }

        if (!await SendAsResolver.IsAllowedSenderAsync(_chatParticipantsRepository, _chatRepository, userId,
                sendAs.Value))
        {
            return Error("SEND_AS_PEER_INVALID");
        }

        _settings.PutDefaultSendAs(userId, destination.Value, sendAs.Value);
        return await _unitOfWork.SaveAsync()
            ? new BoolTrue()
            : Error("INTERNAL_SERVER_ERROR");
    }

    private static TLBool Error(string message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
