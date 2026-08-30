// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class ReportProfilePhotoHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IPhotoRepository _photoRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ModerationStore _moderation;

    public ReportProfilePhotoHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IPhotoRepository photoRepository,
        ModerationStore moderation)
    {
        _authorizationRepository = authorizationRepository;
        _chatRepository = chatRepository;
        _photoRepository = photoRepository;

        _unitOfWork = unitOfWork;
        _moderation = moderation;
    }

    [TLFunction(Constructors.baseLayer_ReportProfilePhoto)]
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

        var request = (ReportProfilePhoto)q;
        bool peerResolved = PeerResolver.TryResolveInputPeerDialogKey(
            request.Get_PeerView(), userId, out DialogPeerKey peer);
        long photoId = request.Get_PhotoIdView().Is(out InputPhoto photo)
            ? photo.Id
            : 0;
        string? reason = ReportReasonToken(request.Get_ReasonView());
        string comment = Encoding.UTF8.GetString(request.Message);

        if (!peerResolved ||
            (peer.Type == TLPeer.PeerType.PeerUser && peer.Id == userId))
        {
            return Error("PEER_ID_INVALID");
        }
        if (reason == null)
        {
            return Error("REPORT_REASON_INVALID");
        }

        string? peerError = await _moderation.ValidateReportablePeerAsync(userId,
            peer.Type, peer.Id);
        if (peerError != null)
        {
            return Error(peerError);
        }

        if (photoId == 0 || !await PhotoBelongsToPeerAsync(peer, photoId))
        {
            return Error("PHOTO_INVALID");
        }

        long reportId = await _moderation.RecordReportAsync(userId,
            ModerationReportKind.ProfilePhoto, peer.Type, peer.Id,
            option: reason, comment: comment, photoId: photoId);
        if (reportId == 0 || !await _unitOfWork.SaveAsync())
        {
            return Error("INTERNAL_SERVER_ERROR");
        }
        return BoolTrue.Builder().Build();
    }

    private async ValueTask<bool> PhotoBelongsToPeerAsync(DialogPeerKey peer,
        long photoId)
    {
        if (peer.Type == TLPeer.PeerType.PeerUser)
        {
            using TLBytes? stored = _photoRepository
                .GetProfilePhoto(peer.Id, photoId);
            return stored != null;
        }

        using TLChat? chat = await _chatRepository.GetChatAsync(peer.Id);
        if (chat == null)
        {
            return false;
        }

        ChatPhotoView photo = chat.Value.Type == TLChat.ChatType.Channel
            ? chat.Value.AsChannel().Get_PhotoView()
            : chat.Value.AsChat().Get_PhotoView();
        return photo.Is(out ChatPhoto current) && current.PhotoId == photoId;
    }

    private static string? ReportReasonToken(ReportReasonView reason) =>
        reason.Type switch
        {
            TLReportReason.ReportReasonType.InputReportReasonSpam => "spam",
            TLReportReason.ReportReasonType.InputReportReasonViolence => "violence",
            TLReportReason.ReportReasonType.InputReportReasonPornography =>
                "pornography",
            TLReportReason.ReportReasonType.InputReportReasonChildAbuse =>
                "child_abuse",
            TLReportReason.ReportReasonType.InputReportReasonOther => "other",
            TLReportReason.ReportReasonType.InputReportReasonCopyright =>
                "copyright",
            TLReportReason.ReportReasonType.InputReportReasonGeoIrrelevant =>
                "geo_irrelevant",
            TLReportReason.ReportReasonType.InputReportReasonFake => "fake",
            TLReportReason.ReportReasonType.InputReportReasonIllegalDrugs =>
                "illegal_drugs",
            TLReportReason.ReportReasonType.InputReportReasonPersonalDetails =>
                "personal_details",
            _ => null,
        };

    private static TLBool Error(string message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
