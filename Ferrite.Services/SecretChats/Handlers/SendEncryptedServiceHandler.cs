// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.SecretChats.Handlers;

public sealed class SendEncryptedServiceHandler : SecretChatSendHandlerBase
{
    private readonly SecretChatTelemetry _telemetry;

    public SendEncryptedServiceHandler(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, ISecretChatsRepository secretChatsRepository, IUserRepository userRepository,
        ISecretChatQtsQueue qtsQueue, SecretChatLimits limits, IMTProtoTime time,
        SecretChatTelemetry telemetry)
        : base(unitOfWork, blockedPeersRepository, authorizationRepository, secretChatsRepository, userRepository, qtsQueue, limits, time)
    {
        _telemetry = telemetry;
    }

    [TLFunction(Constructors.baseLayer_SendEncryptedService)]
    public ValueTask<TLSentEncryptedMessage> Handle(long authKeyId, TLBytes q)
    {
        var request = (SendEncryptedService)q;
        InputEncryptedChatView peerView = request.Get_PeerView();
        if (!peerView.Is(out InputEncryptedChat peer))
        {
            return ValueTask.FromResult(Error());
        }
        _telemetry.ServiceRelay(authKeyId, peer.ChatId, request.Data.Length);
        return SendAsync(authKeyId, peer.ChatId, peer.AccessHash, request.RandomId,
            request.Data.ToArray(), true);
    }

    private static TLSentEncryptedMessage Error() =>
        (TLSentEncryptedMessage)RpcErrorGenerator.GenerateError(400,
            "CHAT_ID_INVALID"u8);
}
