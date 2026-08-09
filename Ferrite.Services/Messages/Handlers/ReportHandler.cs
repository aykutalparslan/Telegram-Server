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
/// The interactive report protocol pinned TDLib drives through `ReportPeerQuery`
/// (`DialogManager.cpp:480`). An empty `option` is the client asking what it may
/// report, so the server answers with the deterministic reason menu; the client
/// then re-invokes the method with one menu token and the report is accepted.
/// Ferrite never asks for a second comment step: `reportResultAddComment` would
/// force another round trip for a comment the client may already have supplied
/// in `message`, and every menu entry is self-explanatory.
/// </summary>
public sealed class ReportHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ModerationStore _moderation;
    private readonly MessageLocator _messages;

    public ReportHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, ModerationStore moderation,
        MessageLocator messages)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _moderation = moderation;
        _messages = messages;
    }

    [TLFunction(Constructors.baseLayer_MessagesReport)]
    public async Task<TLReportResult> Handle(long authKeyId, TLBytes q)
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

        var request = (MessagesReport)q;
        bool resolved = PeerResolver.TryResolveInputPeerDialogKey(
            request.Get_PeerView(), userId, out DialogPeerKey peer);
        List<int> messageIds = ReadIds(request.Id);
        ReadOnlySpan<byte> requested = request.Option;
        string? option = requested.Length == 0
            ? null
            : MessageReportMenu.IsKnownOption(requested)
                ? Encoding.UTF8.GetString(requested)
                : string.Empty;
        string comment = Encoding.UTF8.GetString(request.Message);

        if (!resolved ||
            (peer.Type == TLPeer.PeerType.PeerUser && peer.Id == userId))
        {
            return Error("PEER_ID_INVALID");
        }
        if (option is { Length: 0 })
        {
            return Error("REPORT_OPTION_INVALID");
        }

        string? peerError = await _moderation.ValidateReportablePeerAsync(userId,
            peer.Type, peer.Id);
        if (peerError != null)
        {
            return Error(peerError);
        }

        foreach (int messageId in messageIds)
        {
            if (await _messages.ResolveIdentityAsync(userId, peer.Type, peer.Id,
                    messageId) == null)
            {
                return Error("MESSAGE_ID_INVALID");
            }
        }

        if (option == null)
        {
            return BuildMenu();
        }

        long reportId = await _moderation.RecordReportAsync(userId,
            ModerationReportKind.MessageOption, peer.Type, peer.Id, option: option,
            comment: comment, messageIds: messageIds);
        if (reportId == 0 || !await _unitOfWork.SaveAsync())
        {
            return Error("INTERNAL_SERVER_ERROR");
        }
        return ReportResultReported.Builder().Build();
    }

    private static TLReportResult BuildMenu()
    {
        var options = new Vector();
        foreach ((string token, string text) in MessageReportMenu.Options)
        {
            using MessageReportOption entry = MessageReportOption.Builder()
                .Text(Encoding.UTF8.GetBytes(text))
                .Option(Encoding.UTF8.GetBytes(token))
                .Build();
            options.AppendTLObject(entry.ToReadOnlySpan());
        }

        return ReportResultChooseOption.Builder()
            .Title(Encoding.UTF8.GetBytes(MessageReportMenu.Title))
            .Options(options)
            .Build();
    }

    private static List<int> ReadIds(VectorOfInt ids)
    {
        // Duplicates collapse, but an out-of-range id is kept so validation can
        // reject it rather than quietly accept a report naming no message.
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

    private static TLReportResult Error(string message) =>
        (TLReportResult)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
