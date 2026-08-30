// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class ReportSpamHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ModerationStore _moderation;

    public ReportSpamHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, ModerationStore moderation)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _moderation = moderation;
    }

    [TLFunction(Constructors.baseLayer_MessagesReportSpam)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
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

        var request = (MessagesReportSpam)q;
        bool resolved = PeerResolver.TryResolveInputPeerDialogKey(
            request.Get_PeerView(), userId, out DialogPeerKey peer);

        if (!resolved ||
            (peer.Type == TLPeer.PeerType.PeerUser && peer.Id == userId))
        {
            return Error("PEER_ID_INVALID");
        }

        string? peerError = await _moderation.ValidateReportablePeerAsync(userId,
            peer.Type, peer.Id);
        if (peerError != null)
        {
            return Error(peerError);
        }

        long reportId = await _moderation.RecordReportAsync(userId,
            ModerationReportKind.PeerSpam, peer.Type, peer.Id);
        if (reportId == 0)
        {
            return Error("INTERNAL_SERVER_ERROR");
        }
        if (!await _moderation.SetActionBarAsync(userId, peer.Type, peer.Id,
                hidden: true, reportedSpam: true))
        {
            return Error("INTERNAL_SERVER_ERROR");
        }
        if (!await _unitOfWork.SaveAsync())
        {
            return Error("INTERNAL_SERVER_ERROR");
        }
        return BoolTrue.Builder().Build();
    }

    private static TLBool Error(string message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
