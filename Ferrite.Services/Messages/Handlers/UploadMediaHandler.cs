// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class UploadMediaHandler
{
    private readonly IDocumentsRepository _documentsRepository;
    private readonly IPhotoRepository _photoRepository;

    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IUploadService _upload;
    private readonly IPhotoProcessingService _photos;

    public UploadMediaHandler(IUnitOfWork unitOfWork, IDocumentsRepository documentsRepository, IPhotoRepository photoRepository, IAuthorizationRepository authorizationRepository, IUploadService upload,
        IPhotoProcessingService photos)
    {
        _documentsRepository = documentsRepository;
        _photoRepository = photoRepository;

        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _upload = upload;
        _photos = photos;
    }

    [TLFunction(Constructors.baseLayer_UploadMedia)]
    public async ValueTask<TLMessageMedia> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return Error(ErrorMessages.InvalidAuthKey);
        }

        byte[] mediaBytes;
        {
            var request = (UploadMedia)q;
            long userId = auth.Value.AsAuthInfo().UserId;
            using TLPeer peer = PeerResolver.PeerFromInputPeer(request.Get_PeerView(), userId);
            // TDLib pre-finalizes album items with messages.uploadMedia for the
            // eventual destination peer. The actual send path performs membership
            // and banned-rights checks; uploadMedia only needs a valid peer shape.
            if (GetPeerId(peer) <= 0)
            {
                return Error(ErrorMessages.PeerIdInvalid);
            }
            mediaBytes = request.Media.ToArray();
        }

        MediaResolver.MediaResolution resolution = await MediaResolver.ResolveAsync(
            mediaBytes, _upload, _photos, _unitOfWork, _photoRepository, _documentsRepository);
        if (resolution.Error is { } error)
        {
            return Error(error);
        }
        if (resolution.MediaBytes == null)
        {
            return Error(ErrorMessages.InternalServerError);
        }

        return new TLMessageMedia(resolution.MediaBytes, 0, resolution.MediaBytes.Length);
    }

    private static long GetPeerId(TLPeer peer) => peer.Type switch
    {
        TLPeer.PeerType.PeerUser => peer.AsPeerUser().UserId,
        TLPeer.PeerType.PeerChat => peer.AsPeerChat().ChatId,
        TLPeer.PeerType.PeerChannel => peer.AsPeerChannel().ChannelId,
        _ => 0
    };

    private static TLMessageMedia Error(ErrorMessage error) =>
        (TLMessageMedia)RpcErrorGenerator.GenerateError(error.Code,
            Encoding.UTF8.GetBytes(error.Message));
}
