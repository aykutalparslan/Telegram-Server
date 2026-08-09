// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;

namespace Ferrite.Data.Repositories;

public interface IPhotoRepository
{
    public bool PutProfilePhoto(long userId, long fileId, long accessHash,
        byte[] referenceBytes, DateTimeOffset date);
    public bool DeleteProfilePhoto(long userId, long fileId);
    public IReadOnlyList<TLBytes> GetProfilePhotos(long userId);
    public TLBytes? GetProfilePhoto(long userId, long fileId);
    public bool PutPhoto(TLBytes photo);
    public TLBytes? GetPhoto(long photoId);
    public bool DeletePhoto(long photoId);
    public bool PutThumbnail(TLBytes thumbnail);
    public IReadOnlyList<TLBytes> GetThumbnails(long photoId);
    public bool DeleteThumbnails(long photoId);
}
