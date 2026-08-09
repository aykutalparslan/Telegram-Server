// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Crypto;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.Utils;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.photos;
using Photos = Ferrite.TL.baseLayer.photos.Photos;
using PhotosPhoto = Ferrite.TL.baseLayer.photos.PhotosPhoto;
using PhotosSlice = Ferrite.TL.baseLayer.photos.PhotosSlice;
using TLPhoto = Ferrite.TL.baseLayer.TLPhoto;
using TLPhotos = Ferrite.TL.baseLayer.photos.TLPhotos;
using TLPhotosPhoto = Ferrite.TL.baseLayer.photos.TLPhoto;

namespace Ferrite.Services.Handlers.PhotoMethods;

public sealed class UploadProfilePhotoHandler : PhotosHandlerBase
{
    private readonly IPhotoRepository _photoRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUploadService _upload;
    private readonly IPhotoProcessingService _photoProcessing;

    public UploadProfilePhotoHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IContactsRepository contactsRepository, IPhotoRepository photoRepository, IUserRepository userRepository, IUploadService upload,
        IPhotoProcessingService photoProcessing, IUpdatesService updates, ILogger log)
        : base(unitOfWork, authorizationRepository, contactsRepository, photoRepository, userRepository, updates, log)
    {
        _photoRepository = photoRepository;
        _userRepository = userRepository;

        _upload = upload;
        _photoProcessing = photoProcessing;
    }

    [TLFunction(Constructors.baseLayer_UploadProfilePhoto)]
    public async ValueTask<TLPhotosPhoto> Handle(long authKeyId, TLBytes q)
        {
            var identity = await GetIdentity(authKeyId);
            if (identity == null)
            {
                return PhotoError(ErrorMessages.InvalidAuthKey);
            }

            using TLUser user = identity.Value.User;
            var request = new UploadProfilePhoto(q.AsSpan());
            if (request.Flags[1] || request.Flags[2] || request.Flags[4])
            {
                return PhotoError(ErrorMessages.VideoFileInvalid);
            }
            TLInputFile inputFile;
            if (!request.Flags[0] || !ChatPhotos.TryReadInputFile(request.Get_FileView(), out inputFile))
            {
                return PhotoError(ErrorMessages.PhotoFileMissing);
            }

            ServiceResult<TLUploadedFileInfo?> saved;
            using (inputFile)
            {
                saved = await _upload.SaveFile(inputFile);
            }
            if (!saved.Success || saved.Result == null)
            {
                return PhotoError(saved.ErrorMessage);
            }

            using TLUploadedFileInfo uploaded = saved.Result.Value;
            var processed = await _photoProcessing.ProcessPhoto(uploaded);
            if (!processed.Success || processed.Result == null)
            {
                return PhotoError(processed.ErrorMessage);
            }

            using TLPhoto photo = processed.Result.Value;
            var concrete = photo.AsPhoto();
            long photoId = concrete.Id;
            bool queued = _photoRepository.PutProfilePhoto(identity.Value.UserId,
                photoId, concrete.AccessHash, concrete.FileReference.ToArray(), DateTimeOffset.Now);
            using TLUser updatedUser = WithCurrentPhoto(user, photoId);
            queued = _userRepository.PutUser(updatedUser) && queued;
            if (!queued || !await _unitOfWork.SaveAsync())
            {
                return PhotoError(ErrorMessages.InternalServerError);
            }

            _log.Debug($"📷 UploadProfilePhoto user:{identity.Value.UserId} photo:{photoId}");
            await PushUserInvalidation(identity.Value.UserId);
            return BuildPhotoResult(photo, updatedUser);
        }
}
