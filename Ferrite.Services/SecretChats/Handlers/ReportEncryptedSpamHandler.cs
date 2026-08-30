// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.SecretChats.Handlers;

public sealed class ReportEncryptedSpamHandler : SecretChatHandlerBase
{
    private readonly IReportReasonRepository _reportReasonRepository;

    public ReportEncryptedSpamHandler(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, IReportReasonRepository reportReasonRepository, ISecretChatsRepository secretChatsRepository, IUserRepository userRepository,
        SecretChatLimits limits)
        : base(unitOfWork, blockedPeersRepository, authorizationRepository, secretChatsRepository, userRepository, limits)
    {
        _reportReasonRepository = reportReasonRepository;

    }

    [TLFunction(Constructors.baseLayer_ReportEncryptedSpam)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = (ReportEncryptedSpam)q;
        InputEncryptedChatView peerView = request.Get_PeerView();
        if (!peerView.Is(out InputEncryptedChat peer))
        {
            return Error();
        }
        int chatId = peer.ChatId;
        long accessHash = peer.AccessHash;

        SecretChatPeerResolution resolved = await ResolveActivePeerAsync(authKeyId,
            chatId, accessHash, false, false);
        if (resolved.Status != SecretChatPeerResolutionStatus.Resolved)
        {
            return Error();
        }

        using TLReportReason reason = InputReportReasonSpam.Builder().Build();
        using TLReportReasonWithMessage report = ReportReasonWithMessage.Builder()
            .ReportReason(reason.AsSpan())
            .Message(ReadOnlySpan<byte>.Empty)
            .Build();
        bool stored = _reportReasonRepository.PutPeerReportReason(
            resolved.Context.CallerUserId, (int)PeerType.User,
            resolved.Context.PeerUserId, report);
        return stored && await UnitOfWork.SaveAsync()
            ? BoolTrue.Builder().Build()
            : BoolFalse.Builder().Build();
    }

    private static TLBool Error() =>
        (TLBool)RpcErrorGenerator.GenerateError(400, "CHAT_ID_INVALID"u8);
}
