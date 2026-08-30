// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public class PhotoRepository : IPhotoRepository
{
    private readonly IKVStore _store;
    private readonly IKVStore _storeThumb;
    private readonly IKVStore _storePhotos;
    public PhotoRepository(IKVStore store, IKVStore storeThumb, IKVStore storePhotos)
    {
        _store = store;
        _store.SetSchema(new TableDefinition("ferrite", "profile_photos",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "file_id", Type = DataType.Long })));
        _storeThumb = storeThumb;
        _storeThumb.SetSchema(new TableDefinition("ferrite", "thumbnails",
            new KeyDefinition("pk",
                new DataColumn { Name = "file_id", Type = DataType.Long },
                new DataColumn { Name = "thumb_file_id", Type = DataType.Long },
                new DataColumn { Name = "thumb_type", Type = DataType.String })));
        _storePhotos = storePhotos;
        _storePhotos.SetSchema(new TableDefinition("ferrite", "photos",
            new KeyDefinition("pk",
                new DataColumn { Name = "photo_id", Type = DataType.Long })));
    }
    public bool PutProfilePhoto(long userId, long fileId, long accessHash, byte[] referenceBytes, DateTimeOffset date)
    {
        using var photoBytes = Photo.Builder()
            .Id(fileId)
            .AccessHash(accessHash)
            .FileReference(referenceBytes)
            .Date((int)date.ToUnixTimeSeconds())
            .DcId(MediaDefaults.DcId)
            .Sizes(new Vector()).Build().TLBytes!.Value;
        
        return _store.Put(photoBytes.AsSpan().ToArray(), userId, fileId);
    }

    public bool DeleteProfilePhoto(long userId, long fileId)
    {
        return _store.Delete(userId, fileId);
    }

    public IReadOnlyList<TLBytes> GetProfilePhotos(long userId)
    {
        List<(int Order, TLBytes Photo)> photos = new();
        var iter = _store.Iterate(userId);
        foreach (var photoBytes in iter)
        {
            var association = (Photo)photoBytes.AsSpan();
            byte[]? canonical = _storePhotos.Get(association.Id);
            if (canonical != null)
            {
                photos.Add((association.Date, new TLBytes(canonical, 0, canonical.Length)));
                continue;
            }

            var photoSizes = GetPhotoSizes(association.Id);
            var photo = association.Clone().Sizes(photoSizes).Build();
            photos.Add((association.Date, photo.TLBytes!.Value));
        }

        return photos
            .OrderByDescending(photo => photo.Order)
            .Select(photo => photo.Photo)
            .ToList();
    }

    public TLBytes? GetProfilePhoto(long userId, long fileId)
    {
        var photoBytes = _store.Get(userId, fileId);
        if (photoBytes == null) return null;
        byte[]? canonical = _storePhotos.Get(fileId);
        if (canonical != null)
        {
            return new TLBytes(canonical, 0, canonical.Length);
        }

        var photoSizes = GetPhotoSizes(fileId);
        var photo = ((Photo)photoBytes.AsSpan()).Clone().Sizes(photoSizes).Build();
        return photo.TLBytes!.Value;
    }
    
    private Vector GetPhotoSizes(long photoId)
    {
        var iterThumb = _storeThumb.Iterate(photoId);
        Vector photoSizes = new();
        foreach (var thumbBytes in iterThumb)
        {
            var thumb = (Thumbnail)thumbBytes.AsSpan();
            photoSizes.AppendTLObject(thumb.PhotoSize);
        }

        return photoSizes;
    }

    public bool PutPhoto(TLBytes photo)
    {
        long photoId = ((Photo)photo.AsSpan()).Id;
        return _storePhotos.Put(photo.AsSpan().ToArray(), photoId);
    }

    public TLBytes? GetPhoto(long photoId)
    {
        var photoBytes = _storePhotos.Get(photoId);
        if (photoBytes == null) return null;
        return new TLBytes(photoBytes, 0, photoBytes.Length);
    }

    public bool DeletePhoto(long photoId)
    {
        return _storePhotos.Delete(photoId);
    }

    public bool PutThumbnail(TLBytes thumbnail)
    {
        var thumb = (Thumbnail)thumbnail;
        var photoSize = (PhotoSize)thumb.PhotoSize.ToArray().AsSpan();
        return _storeThumb.Put(thumbnail.AsSpan().ToArray(), thumb.FileId,
            thumb.ThumbFileId, Encoding.UTF8.GetString(photoSize.Type));
    }

    public IReadOnlyList<TLBytes> GetThumbnails(long photoId)
    {
        List<TLBytes> thumbs = new();
        var iter = _storeThumb.Iterate(photoId);
        foreach (var thumbBytes in iter)
        {
            thumbs.Add(new TLBytes(thumbBytes, 0, thumbBytes.Length));
        }

        return thumbs
            .OrderBy(thumb => new PhotoSize(
                ((Thumbnail)thumb.AsSpan()).PhotoSize.ToArray().AsSpan()).W)
            .ToList();
    }

    public bool DeleteThumbnails(long photoId)
    {
        return _storeThumb.Delete(photoId);
    }
}
