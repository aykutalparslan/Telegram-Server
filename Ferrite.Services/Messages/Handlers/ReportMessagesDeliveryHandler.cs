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
/// Records that the client observed delivery of specific messages, the route
/// pinned TDLib takes through `ReportMessageDeliveryQuery`
/// (`MessageQueryManager.cpp:149`) after a push. This is telemetry, not a read
/// marker: it must never advance read state, touch unread counts, or emit an
/// update, so it only appends one durable observation.
/// </summary>
public sealed class ReportMessagesDeliveryHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ModerationStore _moderation;
    private readonly MessageLocator _messages;

    public ReportMessagesDeliveryHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        ModerationStore moderation, MessageLocator messages)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _moderation = moderation;
        _messages = messages;
    }

    [TLFunction(Constructors.baseLayer_ReportMessagesDelivery)]
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

        var request = (ReportMessagesDelivery)q;
        bool resolved = PeerResolver.TryResolveInputPeerDialogKey(
            request.Get_PeerView(), userId, out DialogPeerKey peer);
        bool push = request.Push;
        List<int> messageIds = ReadIds(request.Id);

        if (!resolved)
        {
            return Error("PEER_ID_INVALID");
        }
        if (messageIds.Count == 0)
        {
            return Error("MESSAGE_IDS_EMPTY");
        }

        string? peerError = await _moderation.ValidateReportablePeerAsync(userId,
            peer.Type, peer.Id);
        if (peerError != null)
        {
            return Error(peerError);
        }

        // Every id must be one the caller can already see in this dialog, so a
        // delivery observation can never be used to probe another conversation.
        foreach (int messageId in messageIds)
        {
            if (await _messages.ResolveIdentityAsync(userId, peer.Type, peer.Id,
                    messageId) == null)
            {
                return Error("MESSAGE_ID_INVALID");
            }
        }

        long reportId = await _moderation.RecordReportAsync(userId,
            ModerationReportKind.MessageDelivery, peer.Type, peer.Id,
            option: push ? "push" : null, messageIds: messageIds);
        if (reportId == 0 || !await _unitOfWork.SaveAsync())
        {
            return Error("INTERNAL_SERVER_ERROR");
        }
        return BoolTrue.Builder().Build();
    }

    private static List<int> ReadIds(VectorOfInt ids)
    {
        var messageIds = new List<int>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            if (!messageIds.Contains(ids[i]))
            {
                messageIds.Add(ids[i]);
            }
        }
        return messageIds;
    }

    private static TLBool Error(string message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
