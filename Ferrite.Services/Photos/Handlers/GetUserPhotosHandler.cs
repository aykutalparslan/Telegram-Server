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

public sealed class GetUserPhotosHandler : PhotosHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IPhotoRepository _photoRepository;
    private readonly IUserRepository _userRepository;
    private readonly UserSerializer _userSerializer;

    public GetUserPhotosHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IContactsRepository contactsRepository, IPhotoRepository photoRepository, IUserRepository userRepository, UserSerializer userSerializer, IUpdatesService updates,
        ILogger log)
        : base(unitOfWork, authorizationRepository, contactsRepository, photoRepository, userRepository, updates, log)
    {
        _authorizationRepository = authorizationRepository;
        _photoRepository = photoRepository;
        _userRepository = userRepository;
        _userSerializer = userSerializer;

    }

    [TLFunction(Constructors.baseLayer_GetUserPhotos)]
    public async ValueTask<TLPhotos> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return PhotosError(ErrorMessages.InvalidAuthKey);
            }

            var request = new GetUserPhotos(q.AsSpan());
            long? targetUserId = ResolveInputUser((InputUserView)request.UserId,
                auth.Value.AsAuthInfo().UserId);
            if (targetUserId == null)
            {
                return PhotosError(ErrorMessages.UserIdInvalid);
            }

            using TLUser? user = _userRepository.GetUser(targetUserId.Value);
            if (user == null)
            {
                return PhotosError(ErrorMessages.UserIdInvalid);
            }

            var rows = _photoRepository.GetProfilePhotos(targetUserId.Value);
            int requestedOffset = request.Offset;
            long requestedMaxId = request.MaxId;
            int requestedLimit = request.Limit;
            try
            {
                if (requestedOffset == -1 && requestedMaxId != 0)
                {
                    var exact = rows.Where(x => new Photo(x.AsSpan()).Id == requestedMaxId)
                        .Take(1).ToList();
                    return BuildPhotos(auth.Value.AsAuthInfo().UserId, exact, user.Value, exact.Count, sliced: false,
                        _userSerializer);
                }

                IEnumerable<TLBytes> filtered = rows;
                if (requestedMaxId != 0)
                {
                    filtered = filtered.Where(x => new Photo(x.AsSpan()).Id <= requestedMaxId);
                }
                var all = filtered.ToList();
                int offset = Math.Max(0, requestedOffset);
                int limit = Math.Max(0, requestedLimit);
                var page = all.Skip(offset).Take(limit).ToList();
                bool sliced = offset != 0 || page.Count != all.Count;
                _log.Debug($"📷 GetUserPhotos user:{targetUserId} total:{all.Count} page:{page.Count}");
                return BuildPhotos(auth.Value.AsAuthInfo().UserId, page, user.Value, all.Count, sliced,
                    _userSerializer);
            }
            finally
            {
                foreach (var row in rows) row.Dispose();
            }
        }
}
