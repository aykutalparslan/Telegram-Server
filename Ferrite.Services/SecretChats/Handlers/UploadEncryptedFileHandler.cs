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

public sealed class UploadEncryptedFileHandler : SecretChatHandlerBase
{
    private readonly SecretChatEncryptedFileResolver _files;
    private readonly IMTProtoTime _time;

    public UploadEncryptedFileHandler(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, ISecretChatsRepository secretChatsRepository, IUserRepository userRepository,
        SecretChatEncryptedFileResolver files, SecretChatLimits limits,
        IMTProtoTime time) : base(unitOfWork, blockedPeersRepository, authorizationRepository, secretChatsRepository, userRepository, limits)
    {
        _files = files;
        _time = time;
    }

    [TLFunction(Constructors.baseLayer_UploadEncryptedFile)]
    public async ValueTask<TLEncryptedFile> Handle(long authKeyId, TLBytes q)
    {
        var request = (UploadEncryptedFile)q;
        InputEncryptedChatView peerView = request.Get_PeerView();
        if (!peerView.Is(out InputEncryptedChat peer))
        {
            return Error(400, "CHAT_ID_INVALID"u8);
        }
        SecretChatEncryptedFileInput input = SecretChatEncryptedFileResolver.Parse(
            request.Get_FileView());
        int chatId = peer.ChatId;
        long accessHash = peer.AccessHash;

        SecretChatPeerResolution resolved = await ResolveActivePeerAsync(authKeyId,
            chatId, accessHash);
        if (resolved.Status != SecretChatPeerResolutionStatus.Resolved)
        {
            return PeerError(resolved.Status);
        }

        int date = checked((int)_time.GetUnixTimeInSeconds());
        ServiceResult<TLDto.TLSecretChatEncryptedFile?> result = await _files
            .ResolveAsync(chatId, input, date);
        if (!result.Success || result.Result is null)
        {
            return Error(result.ErrorMessage);
        }
        using TLDto.TLSecretChatEncryptedFile file = result.Result.Value;
        return SecretChatEncryptedFileResolver.BuildWireFile(file);
    }

    private static TLEncryptedFile PeerError(
        SecretChatPeerResolutionStatus status) => status switch
    {
        SecretChatPeerResolutionStatus.Declined =>
            Error(400, "ENCRYPTION_DECLINED"u8),
        SecretChatPeerResolutionStatus.UserDeleted =>
            Error(403, "USER_DELETED"u8),
        SecretChatPeerResolutionStatus.UserBlocked =>
            Error(403, "USER_IS_BLOCKED"u8),
        _ => Error(400, "CHAT_ID_INVALID"u8)
    };

    private static TLEncryptedFile Error(ErrorMessage error) => Error(error.Code,
        Encoding.UTF8.GetBytes(error.Message));

    private static TLEncryptedFile Error(int code, ReadOnlySpan<byte> message) =>
        (TLEncryptedFile)RpcErrorGenerator.GenerateError(code, message);
}
