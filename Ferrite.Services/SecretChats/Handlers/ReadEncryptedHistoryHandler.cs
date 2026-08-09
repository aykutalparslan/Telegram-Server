// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.SecretChats.Handlers;

public sealed class ReadEncryptedHistoryHandler : SecretChatHandlerBase
{
    private readonly ISecretChatsRepository _secretChatsRepository;

    private readonly SecretChatControlDelivery _controlDelivery;
    private readonly IMTProtoTime _time;

    public ReadEncryptedHistoryHandler(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, ISecretChatsRepository secretChatsRepository, IUserRepository userRepository,
        SecretChatLimits limits, SecretChatControlDelivery controlDelivery,
        IMTProtoTime time)
        : base(unitOfWork, blockedPeersRepository, authorizationRepository, secretChatsRepository, userRepository, limits)
    {
        _secretChatsRepository = secretChatsRepository;

        _controlDelivery = controlDelivery;
        _time = time;
    }

    [TLFunction(Constructors.baseLayer_ReadEncryptedHistory)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = (ReadEncryptedHistory)q;
        InputEncryptedChatView peerView = request.Get_PeerView();
        if (!peerView.Is(out InputEncryptedChat peer))
        {
            return Error("CHAT_ID_INVALID"u8);
        }
        int chatId = peer.ChatId;
        long accessHash = peer.AccessHash;
        int maxDate = request.MaxDate;
        if (maxDate <= 0)
        {
            return Error("MAX_DATE_INVALID"u8);
        }

        SecretChatPeerResolution resolved = await ResolveActivePeerAsync(authKeyId,
            chatId, accessHash, false, false);
        if (resolved.Status != SecretChatPeerResolutionStatus.Resolved)
        {
            return Error("CHAT_ID_INVALID"u8);
        }

        int date = checked((int)_time.GetUnixTimeInSeconds());
        byte[] updateBytes;
        using (TLUpdate update = UpdateEncryptedMessagesRead.Builder()
                   .ChatId(chatId)
                   .MaxDate(maxDate)
                   .Date(date)
                   .Build())
        {
            updateBytes = update.AsSpan().ToArray();
        }
        SecretChatPeerContext context = resolved.Context;
        long updateId = SecretChatControlDelivery.CreateUpdateId(chatId,
            SecretChatControlKind.Read);
        using TLDto.TLSecretChatControlUpdate control =
            TLDto.SecretChatControlUpdate.Builder()
                .RecipientAuthKeyId(context.PeerAuthKeyId)
                .UpdateId(updateId)
                .ChatId(chatId)
                .PeerAuthKeyId(authKeyId)
                .PeerUserId(context.CallerUserId)
                .Date(date)
                .ExpiresAt(checked(date + Limits.QtsRetentionSeconds))
                .Update(updateBytes)
                .Build();
        SecretChatReadAdvanceStatus status = await _secretChatsRepository.AdvanceReadDateAsync(authKeyId, chatId,
                accessHash, maxDate, control);
        if (status == SecretChatReadAdvanceStatus.Advanced)
        {
            await _controlDelivery.DeliverPersistedAsync(context.PeerAuthKeyId,
                context.PeerUserId, chatId,
                new TLUpdate(updateBytes, 0, updateBytes.Length));
        }
        return status is SecretChatReadAdvanceStatus.Advanced or
            SecretChatReadAdvanceStatus.Unchanged
            ? BoolTrue.Builder().Build()
            : Error("CHAT_ID_INVALID"u8);
    }

    private static TLBool Error(ReadOnlySpan<byte> message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400, message);
}
