// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Crypto;
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

public sealed class UpdateProfilePhotoHandler : PhotosHandlerBase
{
    private readonly IPhotoRepository _photoRepository;
    private readonly IUserRepository _userRepository;
    private readonly UserSerializer _userSerializer;

    public UpdateProfilePhotoHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IContactsRepository contactsRepository, IPhotoRepository photoRepository, IUserRepository userRepository, UserSerializer userSerializer, IUpdatesService updates,
        ILogger log)
        : base(unitOfWork, authorizationRepository, contactsRepository, photoRepository, userRepository, updates, log)
    {
        _photoRepository = photoRepository;
        _userRepository = userRepository;
        _userSerializer = userSerializer;

    }

    [TLFunction(Constructors.baseLayer_UpdateProfilePhoto)]
    public async ValueTask<TLPhotosPhoto> Handle(long authKeyId, TLBytes q)
        {
            var identity = await GetIdentity(authKeyId);
            if (identity == null)
            {
                return PhotoError(ErrorMessages.InvalidAuthKey);
            }

            using TLUser user = identity.Value.User;
            var input = (InputPhotoView)new UpdateProfilePhoto(q.AsSpan()).Id;
            if (input.Is(out InputPhotoEmpty _))
            {
                long? currentId = GetCurrentPhotoId(user);
                var ownedRows = _photoRepository.GetProfilePhotos(identity.Value.UserId);
                try
                {
                    bool deleteQueued = true;
                    TLBytes? promotedRow = null;
                    long? promotedId = null;
                    if (currentId != null)
                    {
                        deleteQueued = _photoRepository.DeleteProfilePhoto(
                            identity.Value.UserId, currentId.Value);
                        foreach (var row in ownedRows)
                        {
                            if (new Photo(row.AsSpan()).Id != currentId.Value)
                            {
                                promotedRow = row;
                                promotedId = new Photo(row.AsSpan()).Id;
                                break;
                            }
                        }
                    }

                    using TLUser cleared = WithCurrentPhoto(user, promotedId);
                    if (!deleteQueued || !_userRepository.PutUser(cleared) ||
                        !await _unitOfWork.SaveAsync())
                    {
                        return PhotoError(ErrorMessages.InternalServerError);
                    }
                    _log.Debug($"📷 UpdateProfilePhoto user:{identity.Value.UserId} empty " +
                               $"deleted:{currentId} promoted:{promotedId}");
                    await PushUserInvalidation(identity.Value.UserId);
                    if (promotedRow != null)
                    {
                        using TLPhoto promoted = CopyPhoto(promotedRow.Value);
                        return BuildPhotoResult(identity.Value.UserId, promoted, cleared, _userSerializer);
                    }
                    using TLPhoto empty = PhotoEmpty.Builder().Id(0).Build();
                    return BuildPhotoResult(identity.Value.UserId, empty, cleared, _userSerializer);
                }
                finally
                {
                    foreach (var row in ownedRows) row.Dispose();
                }
            }
            if (!input.Is(out InputPhoto requested))
            {
                return PhotoError(ErrorMessages.PhotoIdInvalid);
            }

            using TLBytes? owned = _photoRepository.GetProfilePhoto(
                identity.Value.UserId, requested.Id);
            using TLBytes? canonical = _photoRepository.GetPhoto(requested.Id);
            if (owned == null || canonical == null)
            {
                return PhotoError(ErrorMessages.PhotoIdInvalid);
            }

            var stored = new Photo(canonical.Value.AsSpan());
            if (stored.Constructor != Constructors.baseLayer_Photo ||
                stored.AccessHash != requested.AccessHash ||
                !stored.FileReference.SequenceEqual(requested.FileReference))
            {
                return PhotoError(ErrorMessages.PhotoIdInvalid);
            }

            bool queued = _photoRepository.PutProfilePhoto(identity.Value.UserId,
                stored.Id, stored.AccessHash, stored.FileReference.ToArray(), DateTimeOffset.Now);
            using TLUser updatedUser = WithCurrentPhoto(user, stored.Id);
            queued = _userRepository.PutUser(updatedUser) && queued;
            if (!queued || !await _unitOfWork.SaveAsync())
            {
                return PhotoError(ErrorMessages.InternalServerError);
            }

            await PushUserInvalidation(identity.Value.UserId);
            using TLPhoto resultPhoto = CopyPhoto(canonical.Value);
            return BuildPhotoResult(identity.Value.UserId, resultPhoto, updatedUser, _userSerializer);
        }
}
