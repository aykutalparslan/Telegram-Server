// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.SecretChats.Handlers;

public sealed class SendEncryptedFileHandler : SecretChatSendHandlerBase
{
    private readonly SecretChatEncryptedFileResolver _files;

    public SendEncryptedFileHandler(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, ISecretChatsRepository secretChatsRepository, IUserRepository userRepository,
        ISecretChatQtsQueue qtsQueue, SecretChatEncryptedFileResolver files,
        SecretChatLimits limits, IMTProtoTime time)
        : base(unitOfWork, blockedPeersRepository, authorizationRepository, secretChatsRepository, userRepository, qtsQueue, limits, time)
    {
        _files = files;
    }

    [TLFunction(Constructors.baseLayer_SendEncryptedFile)]
    public ValueTask<TLSentEncryptedMessage> Handle(long authKeyId, TLBytes q)
    {
        var request = (SendEncryptedFile)q;
        InputEncryptedChatView peerView = request.Get_PeerView();
        if (!peerView.Is(out InputEncryptedChat peer))
        {
            return ValueTask.FromResult(Error(400, "CHAT_ID_INVALID"u8));
        }
        SecretChatEncryptedFileInput file = SecretChatEncryptedFileResolver.Parse(
            request.Get_FileView());
        return HandleAsync(authKeyId, peer.ChatId, peer.AccessHash,
            request.RandomId, request.Data.ToArray(), file);
    }

    private async ValueTask<TLSentEncryptedMessage> HandleAsync(long authKeyId,
        int chatId, long accessHash, long randomId, byte[] data,
        SecretChatEncryptedFileInput input)
    {
        SecretChatSendPreparation preparation = await PrepareSendAsync(authKeyId,
            chatId, accessHash, randomId, data);
        if (!preparation.Ready)
        {
            return FromCompleted(preparation);
        }

        int date = CurrentDate;
        ServiceResult<TLDto.TLSecretChatEncryptedFile?> resolved = await _files
            .ResolveAsync(chatId, input, date);
        if (!resolved.Success || resolved.Result is null)
        {
            return Error(resolved.ErrorMessage.Code,
                Encoding.UTF8.GetBytes(resolved.ErrorMessage.Message));
        }

        using TLDto.TLSecretChatEncryptedFile stored = resolved.Result.Value;
        using TLEncryptedFile file = SecretChatEncryptedFileResolver
            .BuildWireFile(stored);
        byte[] fileBytes = file.AsSpan().ToArray();
        byte[] resultBytes;
        using (TLSentEncryptedMessage sent = SentEncryptedFile.Builder()
                   .Date(date)
                   .File(fileBytes)
                   .Build())
        {
            resultBytes = sent.AsSpan().ToArray();
        }
        TLEncryptedMessage message = EncryptedMessage.Builder()
            .RandomId(randomId)
            .ChatId(chatId)
            .Date(date)
            .Bytes(data)
            .File(fileBytes)
            .Build();
        return await EnqueuePreparedAsync(authKeyId, preparation.Context, chatId,
            accessHash, randomId, date, message, resultBytes);
    }
}
