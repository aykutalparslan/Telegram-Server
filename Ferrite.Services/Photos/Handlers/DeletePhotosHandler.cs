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

public sealed class DeletePhotosHandler : PhotosHandlerBase
{
    public DeletePhotosHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IContactsRepository contactsRepository, IPhotoRepository photoRepository, IUserRepository userRepository, IUpdatesService updates,
        ILogger log)
        : base(unitOfWork, authorizationRepository, contactsRepository, photoRepository, userRepository, updates, log)
    {
    }

    [TLFunction(Constructors.baseLayer_DeletePhotos)]
    public async ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
        {
            ServiceResult<IReadOnlyCollection<long>> deleted =
                await DeletePhotosCore(authKeyId, q);
            return deleted.Success
                ? ToLongVector(deleted.Result ?? Array.Empty<long>())
                : RpcErrorGenerator.GenerateError(deleted.ErrorMessage.Code,
                    Encoding.UTF8.GetBytes(deleted.ErrorMessage.Message));
        }
}
