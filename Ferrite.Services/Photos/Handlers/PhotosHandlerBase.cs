// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.photos;
using Ferrite.Utils;
using Photos = Ferrite.TL.baseLayer.photos.Photos;
using PhotosPhoto = Ferrite.TL.baseLayer.photos.PhotosPhoto;
using PhotosSlice = Ferrite.TL.baseLayer.photos.PhotosSlice;
using TLPhoto = Ferrite.TL.baseLayer.TLPhoto;
using TLPhotos = Ferrite.TL.baseLayer.photos.TLPhotos;
using TLPhotosPhoto = Ferrite.TL.baseLayer.photos.TLPhoto;

namespace Ferrite.Services.Handlers.PhotoMethods;

public abstract class PhotosHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IContactsRepository _contactsRepository;
    private readonly IPhotoRepository _photoRepository;
    private readonly IUserRepository _userRepository;

    protected readonly IUnitOfWork _unitOfWork;
    protected readonly IUpdatesService _updates;
    protected readonly ILogger _log;

    protected PhotosHandlerBase(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IContactsRepository contactsRepository, IPhotoRepository photoRepository, IUserRepository userRepository, IUpdatesService updates,
        ILogger log)
    {
        _authorizationRepository = authorizationRepository;
        _contactsRepository = contactsRepository;
        _photoRepository = photoRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _updates = updates;
        _log = log;
    }

    protected async ValueTask<ServiceResult<IReadOnlyCollection<long>>> DeletePhotosCore(
        long authKeyId, TLBytes q)
    {
        var identity = await GetIdentity(authKeyId);
        if (identity == null)
        {
            return new ServiceResult<IReadOnlyCollection<long>>(null, false,
                ErrorMessages.InvalidAuthKey);
        }

        using TLUser user = identity.Value.User;
        var ownedRows = _photoRepository
            .GetProfilePhotos(identity.Value.UserId);
        try
        {
            var ownedIds = ownedRows.Select(x => new Photo(x.AsSpan()).Id).ToHashSet();
            var requestedIds = ReadPhotoIds(new DeletePhotos(q.AsSpan()).Id);
            var deleted = requestedIds.Where(ownedIds.Contains).Distinct().ToList();
            _log.Debug($"📷 DeletePhotos user:{identity.Value.UserId} " +
                       $"requested:[{string.Join(',', requestedIds)}] " +
                       $"owned:[{string.Join(',', ownedIds)}] deleted:{deleted.Count}");
            if (deleted.Count == 0)
            {
                return new ServiceResult<IReadOnlyCollection<long>>(deleted, true,
                    ErrorMessages.None);
            }

            bool queued = true;
            foreach (long photoId in deleted)
            {
                queued = _photoRepository.DeleteProfilePhoto(
                    identity.Value.UserId, photoId) && queued;
            }

            long? currentId = GetCurrentPhotoId(user);
            bool currentDeleted = currentId != null && deleted.Contains(currentId.Value);
            TLUser? updatedUser = null;
            if (currentDeleted)
            {
                long? replacement = ownedRows.Select(x => new Photo(x.AsSpan()).Id)
                    .FirstOrDefault(id => !deleted.Contains(id));
                if (replacement == 0) replacement = null;
                updatedUser = WithCurrentPhoto(user, replacement);
                queued = _userRepository.PutUser(updatedUser.Value) && queued;
            }

            try
            {
                if (!queued || !await _unitOfWork.SaveAsync())
                {
                    return new ServiceResult<IReadOnlyCollection<long>>(null, false,
                        ErrorMessages.InternalServerError);
                }
                if (currentDeleted) await PushUserInvalidation(identity.Value.UserId);
                return new ServiceResult<IReadOnlyCollection<long>>(deleted, true,
                    ErrorMessages.None);
            }
            finally
            {
                updatedUser?.Dispose();
            }
        }
        finally
        {
            foreach (var row in ownedRows) row.Dispose();
        }
    }

    protected static TLBytes ToLongVector(IEnumerable<long> values)
    {
        var vector = new VectorOfLong();
        foreach (long value in values) vector.Append(value);
        byte[] bytes = vector.ToReadOnlySpan().ToArray();
        return new TLBytes(bytes, 0, bytes.Length);
    }

    protected async ValueTask<Identity?> GetIdentity(long authKeyId)
    {
        using var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null) return null;
        long userId = auth.Value.AsAuthInfo().UserId;
        TLUser? stored = _userRepository.GetUser(userId);
        if (stored == null) return null;
        // The store-read user is byte[]-backed; transfer it into the Identity
        // instead of copying its bytes into a fresh TLUser.
        return new Identity(userId, stored.Value);
    }

    protected async Task PushUserInvalidation(long userId)
    {
        var targets = _contactsRepository.GetContactOwners(userId)
            .Append(userId).Distinct().ToList();
        foreach (long target in targets)
        {
            TLUpdate update = UpdateUser.Builder().UserId(userId).Build();
            await _updates.EnqueueUpdate(target, update);
        }
    }

    protected static TLUser WithCurrentPhoto(TLUser user, long? photoId)
    {
        if (photoId == null)
        {
            using var empty = UserProfilePhotoEmpty.Builder().Build();
            return user.AsUser().Clone().Photo(empty.ToReadOnlySpan()).Build();
        }
        using var photo = UserProfilePhoto.Builder().PhotoId(photoId.Value)
            .DcId(MediaDefaults.DcId).Build();
        return user.AsUser().Clone().Photo(photo.ToReadOnlySpan()).Build();
    }

    protected static long? GetCurrentPhotoId(TLUser user)
    {
        var photo = user.AsUser().Get_PhotoView();
        return photo.Is(out UserProfilePhoto current) ? current.PhotoId : null;
    }

    protected static TLPhotosPhoto BuildPhotoResult(TLPhoto photo, TLUser user)
    {
        var users = new Vector();
        users.AppendTLObject(user.AsSpan());
        return PhotosPhoto.Builder().Photo(photo.AsSpan()).Users(users).Build();
    }

    protected static TLPhotos BuildPhotos(IReadOnlyCollection<TLBytes> rows,
        TLUser user, int totalCount, bool sliced)
    {
        var photos = new Vector();
        foreach (var row in rows) photos.AppendTLObject(row.AsSpan());
        var users = new Vector();
        users.AppendTLObject(user.AsSpan());
        return sliced
            ? PhotosSlice.Builder().Count(totalCount).Photos(photos).Users(users).Build()
            : Photos.Builder().PhotosProperty(photos).Users(users).Build();
    }

    protected static List<long> ReadPhotoIds(Vector values)
    {
        var result = new List<long>();
        for (int i = 0; i < values.Count; i++)
        {
            var photo = (InputPhotoView)values.ReadTLObject();
            if (photo.Is(out InputPhoto input)) result.Add(input.Id);
        }
        return result;
    }

    protected static long? ResolveInputUser(InputUserView user, long selfUserId)
    {
        if (user.Is(out InputUserSelf _)) return selfUserId;
        if (user.Is(out InputUser input)) return input.UserId;
        if (user.Is(out InputUserFromMessage fromMessage)) return fromMessage.UserId;
        return null;
    }

    protected static TLPhoto CopyPhoto(TLBytes photo)
    {
        byte[] bytes = photo.AsSpan().ToArray();
        return new TLPhoto(bytes, 0, bytes.Length);
    }

    protected static TLPhotosPhoto PhotoError(ErrorMessage error) =>
        (TLPhotosPhoto)RpcErrorGenerator.GenerateError(error.Code,
            Encoding.UTF8.GetBytes(error.Message));

    protected static TLPhotos PhotosError(ErrorMessage error) =>
        (TLPhotos)RpcErrorGenerator.GenerateError(error.Code,
            Encoding.UTF8.GetBytes(error.Message));

    protected readonly record struct Identity(long UserId, TLUser User);
}
