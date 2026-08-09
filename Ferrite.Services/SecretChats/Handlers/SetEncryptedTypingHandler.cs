// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.SecretChats.Handlers;

public sealed class SetEncryptedTypingHandler : SecretChatHandlerBase
{
    private readonly IUpdatesService _updates;

    public SetEncryptedTypingHandler(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, ISecretChatsRepository secretChatsRepository, IUserRepository userRepository,
        SecretChatLimits limits, IUpdatesService updates)
        : base(unitOfWork, blockedPeersRepository, authorizationRepository, secretChatsRepository, userRepository, limits)
    {
        _updates = updates;
    }

    [TLFunction(Constructors.baseLayer_SetEncryptedTyping)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = (SetEncryptedTyping)q;
        InputEncryptedChatView peerView = request.Get_PeerView();
        if (!peerView.Is(out InputEncryptedChat peer))
        {
            return Error();
        }
        int chatId = peer.ChatId;
        long accessHash = peer.AccessHash;
        bool typing = request.Typing;

        SecretChatPeerResolution resolved = await ResolveActivePeerAsync(authKeyId,
            chatId, accessHash, false, false);
        if (resolved.Status != SecretChatPeerResolutionStatus.Resolved)
        {
            return Error();
        }
        if (!typing)
        {
            return BoolTrue.Builder().Build();
        }

        using TLUpdate update = UpdateEncryptedChatTyping.Builder()
            .ChatId(chatId)
            .Build();
        await _updates.EnqueueUpdate(resolved.Context.PeerUserId, update,
            UpdateDeliveryScope.ForAuthKey(resolved.Context.PeerAuthKeyId));
        return BoolTrue.Builder().Build();
    }

    private static TLBool Error() =>
        (TLBool)RpcErrorGenerator.GenerateError(400, "CHAT_ID_INVALID"u8);
}
