// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

/// <summary>
/// Reports the reaction one user left on one message, the route pinned TDLib
/// takes through `ReportReactionQuery` (`MessageReaction.cpp:324`). The client
/// only requires "know" access to the reacting peer, so the report is accepted
/// even after the reaction itself was retracted: the moderator judges what the
/// reporter saw, not what the message currently holds.
/// </summary>
public sealed class ReportReactionHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ModerationStore _moderation;
    private readonly MessageLocator _messages;

    public ReportReactionHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, ModerationStore moderation,
        MessageLocator messages)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _moderation = moderation;
        _messages = messages;
    }

    [TLFunction(Constructors.baseLayer_ReportReaction)]
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

        var request = (ReportReaction)q;
        bool peerResolved = PeerResolver.TryResolveInputPeerDialogKey(
            request.Get_PeerView(), userId, out DialogPeerKey peer);
        bool reactorResolved = PeerResolver.TryResolveInputPeerDialogKey(
            request.Get_ReactionPeerView(), userId, out DialogPeerKey reactor);
        int messageId = request.Id;

        if (!peerResolved)
        {
            return Error("PEER_ID_INVALID");
        }
        // A reaction is always chosen by a user in Ferrite, and reporting your
        // own reaction is not a report.
        if (!reactorResolved || reactor.Type != TLPeer.PeerType.PeerUser ||
            reactor.Id == userId)
        {
            return Error("PEER_ID_INVALID");
        }

        string? peerError = await _moderation.ValidateReportablePeerAsync(userId,
            peer.Type, peer.Id);
        if (peerError != null)
        {
            return Error(peerError);
        }
        string? reactorError = await _moderation.ValidateReportablePeerAsync(userId,
            reactor.Type, reactor.Id);
        if (reactorError != null)
        {
            return Error("USER_ID_INVALID");
        }

        if (await _messages.ResolveIdentityAsync(userId, peer.Type, peer.Id,
                messageId) == null)
        {
            return Error("MESSAGE_ID_INVALID");
        }

        long reportId = await _moderation.RecordReportAsync(userId,
            ModerationReportKind.Reaction, peer.Type, peer.Id,
            messageIds: [messageId], subjectUserId: reactor.Id);
        if (reportId == 0 || !await _unitOfWork.SaveAsync())
        {
            return Error("INTERNAL_SERVER_ERROR");
        }
        return BoolTrue.Builder().Build();
    }

    private static TLBool Error(string message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
