// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using TLPhoto = Ferrite.TL.baseLayer.TLPhoto;

namespace Ferrite.Services.Media;

public static class MediaResolver
{
    public enum MediaCategory
    {
        Invalid,
        Photo,
        Document,
        Geo,
        Poll,
    }

    public readonly record struct MediaResolution(byte[]? MediaBytes,
        MediaCategory Category, ErrorMessage? Error)
    {
        public static MediaResolution Ok(byte[] bytes, MediaCategory category) =>
            new(bytes, category, null);
        public static MediaResolution Fail(ErrorMessage error) =>
            new(null, MediaCategory.Invalid, error);
    }

    private enum MediaKind
    {
        Invalid, Empty, UploadedPhoto, UploadedDoc, ReusedPhoto, ReusedDoc, Geo,
        GeoLive,
    }

    private readonly record struct GeoSnapshot(bool Present, double Lat,
        double Longitude, int AccuracyRadius);

    public static async ValueTask<MediaResolution> ResolveAsync(byte[] inputMediaBytes,
        IUploadService upload, IPhotoProcessingService photos, IUnitOfWork unitOfWork,
        IPhotoRepository photoRepository,
        IDocumentsRepository documentsRepository)
    {
        MediaKind kind;
        TLInputFile? uploadedFile = null;
        TLInputFile? uploadedThumb = null;
        byte[] docMime = Array.Empty<byte>();
        byte[] docAttributes = Array.Empty<byte>();
        long reuseId = 0;
        long reuseHash = 0;
        byte[] reuseReference = Array.Empty<byte>();
        bool spoiler = false;
        int ttlSeconds = -1;
        GeoSnapshot geo = default;
        int livePeriod = 0;
        int heading = -1;
        int proximityRadius = -1;

        var view = (InputMediaView)inputMediaBytes.AsSpan();
        if (view.Is(out InputMediaUploadedPhoto up) &&
            ChatPhotos.TryReadInputFile(up.Get_FileView(), out var photoFile))
        {
            kind = MediaKind.UploadedPhoto;
            uploadedFile = photoFile;
            spoiler = up.Spoiler;
            ttlSeconds = up.Flags[1] ? up.TtlSeconds : -1;
        }
        else if (view.Is(out InputMediaUploadedDocument ud) &&
                 ChatPhotos.TryReadInputFile(ud.Get_FileView(), out var docFile))
        {
            kind = MediaKind.UploadedDoc;
            uploadedFile = docFile;
            docMime = ud.MimeType.ToArray();
            docAttributes = ud.Attributes.ToReadOnlySpan().ToArray();
            spoiler = ud.Spoiler;
            ttlSeconds = ud.Flags[1] ? ud.TtlSeconds : -1;
            if (ud.Flags[2] && ChatPhotos.TryReadInputFile(ud.Get_ThumbView(), out var thumbFile))
            {
                uploadedThumb = thumbFile;
            }
        }
        else if (view.Is(out InputMediaPhoto mp) && mp.Get_IdView().Is(out InputPhoto photoId))
        {
            kind = MediaKind.ReusedPhoto;
            reuseId = photoId.Id;
            reuseHash = photoId.AccessHash;
            reuseReference = photoId.FileReference.ToArray();
            spoiler = mp.Spoiler;
            ttlSeconds = mp.Flags[0] ? mp.TtlSeconds : -1;
        }
        else if (view.Is(out InputMediaDocument mdoc) && mdoc.Get_IdView().Is(out InputDocument docId))
        {
            kind = MediaKind.ReusedDoc;
            reuseId = docId.Id;
            reuseHash = docId.AccessHash;
            reuseReference = docId.FileReference.ToArray();
            spoiler = mdoc.Spoiler;
            ttlSeconds = mdoc.Flags[0] ? mdoc.TtlSeconds : -1;
        }
        else if (view.Is(out InputMediaGeoPoint point))
        {
            kind = MediaKind.Geo;
            geo = ReadGeo(point.Get_GeoPointView());
        }
        else if (view.Is(out InputMediaGeoLive live))
        {
            kind = live.Stopped || !live.Flags[1]
                ? MediaKind.Geo
                : MediaKind.GeoLive;
            geo = ReadGeo(live.Get_GeoPointView());
            livePeriod = live.Period;
            heading = live.Flags[2] ? live.Heading : -1;
            proximityRadius = live.Flags[3]
                ? live.ProximityNotificationRadius
                : -1;
            if (live.Stopped)
            {
                kind = MediaKind.GeoLive;
                livePeriod = 0;
            }
        }
        else if (view.Is(out InputMediaEmpty _))
        {
            kind = MediaKind.Empty;
        }
        else
        {
            kind = MediaKind.Invalid;
        }

        try
        {
        switch (kind)
        {
            case MediaKind.Empty:
                return MediaResolution.Fail(ErrorMessages.MediaEmpty);
            case MediaKind.Invalid:
                return MediaResolution.Fail(ErrorMessages.MediaInvalid);
            case MediaKind.Geo:
            {
                if (!geo.Present)
                {
                    return MediaResolution.Fail(ErrorMessages.MediaInvalid);
                }
                using TLGeoPoint geoPoint = BuildGeoPoint(geo);
                using TLMessageMedia media = MessageMediaGeo.Builder()
                    .Geo(geoPoint.AsSpan())
                    .Build();
                return MediaResolution.Ok(media.AsSpan().ToArray(),
                    MediaCategory.Geo);
            }
            case MediaKind.GeoLive:
            {
                using TLGeoPoint geoPoint = geo.Present
                    ? BuildGeoPoint(geo)
                    : GeoPointEmpty.Builder().Build();
                var builder = MessageMediaGeoLive.Builder()
                    .Geo(geoPoint.AsSpan())
                    .Period(livePeriod);
                if (heading >= 0) builder = builder.Heading(heading);
                if (proximityRadius >= 0)
                {
                    builder = builder.ProximityNotificationRadius(proximityRadius);
                }
                using TLMessageMedia media = builder.Build();
                return MediaResolution.Ok(media.AsSpan().ToArray(),
                    MediaCategory.Geo);
            }
            case MediaKind.UploadedPhoto:
            {
                var saved = await upload.SaveFile(uploadedFile!.Value);
                if (!saved.Success || saved.Result == null)
                {
                    return MediaResolution.Fail(saved.ErrorMessage);
                }
                using TLUploadedFileInfo info = saved.Result.Value;
                var processed = await photos.ProcessPhoto(info);
                if (!processed.Success || processed.Result == null)
                {
                    return MediaResolution.Fail(processed.ErrorMessage);
                }
                using TLPhoto photo = processed.Result.Value;
                return MediaResolution.Ok(BuildPhotoMedia(photo.AsSpan().ToArray(), spoiler,
                    ttlSeconds), MediaCategory.Photo);
            }
            case MediaKind.UploadedDoc:
            {
                var saved = await upload.SaveFile(uploadedFile!.Value);
                if (!saved.Success || saved.Result == null)
                {
                    return MediaResolution.Fail(saved.ErrorMessage);
                }
                using TLUploadedFileInfo info = saved.Result.Value;
                byte[]? thumbsBytes = uploadedThumb == null
                    ? null
                    : await ProcessDocumentThumb(uploadedThumb.Value,
                        info.AsUploadedFileInfo().Id,
                        upload, photos, unitOfWork, photoRepository);
                var registered = await upload.RegisterDocument(info, docMime, docAttributes,
                    thumbsBytes);
                if (!registered.Success || registered.Result == null)
                {
                    return MediaResolution.Fail(registered.ErrorMessage);
                }
                using TLBytes document = registered.Result.Value;
                return MediaResolution.Ok(BuildDocumentMedia(document.AsSpan().ToArray(), spoiler,
                    ttlSeconds), MediaCategory.Document);
            }
            case MediaKind.ReusedPhoto:
            {
                using TLBytes? stored = photoRepository.GetPhoto(reuseId);
                if (stored == null)
                {
                    return MediaResolution.Fail(ErrorMessages.MediaInvalid);
                }
                var photo = (Photo)stored.Value.AsSpan();
                if (photo.Constructor != Constructors.baseLayer_Photo ||
                    photo.AccessHash != reuseHash ||
                    !photo.FileReference.SequenceEqual(reuseReference))
                {
                    return MediaResolution.Fail(ErrorMessages.MediaInvalid);
                }
                return MediaResolution.Ok(BuildPhotoMedia(stored.Value.AsSpan().ToArray(), spoiler,
                    ttlSeconds), MediaCategory.Photo);
            }
            default:
            {
                using TLBytes? stored = documentsRepository.GetDocument(reuseId);
                if (stored == null)
                {
                    return MediaResolution.Fail(ErrorMessages.MediaInvalid);
                }
                var document = (Document)stored.Value.AsSpan();
                if (document.Constructor != Constructors.baseLayer_Document ||
                    document.AccessHash != reuseHash ||
                    !document.FileReference.SequenceEqual(reuseReference))
                {
                    return MediaResolution.Fail(ErrorMessages.MediaInvalid);
                }
                return MediaResolution.Ok(BuildDocumentMedia(stored.Value.AsSpan().ToArray(),
                    spoiler, ttlSeconds), MediaCategory.Document);
            }
        }
        }
        finally
        {
            uploadedFile?.Dispose();
            uploadedThumb?.Dispose();
        }
    }

    public static MediaCategory GetCategory(Span<byte> inputMediaBytes)
    {
        var view = (InputMediaView)inputMediaBytes;
        if (view.Is(out InputMediaUploadedPhoto _) || view.Is(out InputMediaPhoto _))
        {
            return MediaCategory.Photo;
        }
        if (view.Is(out InputMediaUploadedDocument _) || view.Is(out InputMediaDocument _))
        {
            return MediaCategory.Document;
        }
        if (view.Is(out InputMediaGeoPoint _) || view.Is(out InputMediaGeoLive _))
        {
            return MediaCategory.Geo;
        }
        if (view.Is(out InputMediaPoll _))
        {
            return MediaCategory.Poll;
        }
        return MediaCategory.Invalid;
    }

    public static byte[] ApplyLiveLocationStop(byte[] resolvedMediaBytes,
        Span<byte> previousMediaBytes, int messageDate, int now)
    {
        var view = (MessageMediaView)resolvedMediaBytes.AsSpan();
        if (!view.Is(out MessageMediaGeoLive stopped) || stopped.Period != 0)
        {
            return resolvedMediaBytes;
        }

        Span<byte> geo = stopped.Geo;
        var previous = (MessageMediaView)previousMediaBytes;
        if (((GeoPointView)geo).Is(out GeoPointEmpty _) &&
            previous.Is(out MessageMediaGeoLive before))
        {
            geo = before.Geo;
        }
        using TLMessageMedia media = MessageMediaGeoLive.Builder()
            .Geo(geo)
            .Period(Math.Max(0, now - messageDate))
            .Build();
        return media.AsSpan().ToArray();
    }

    private static GeoSnapshot ReadGeo(InputGeoPointView view) =>
        view.Is(out InputGeoPoint point)
            ? new GeoSnapshot(true, point.Lat, point.Longitude,
                point.Flags[0] ? point.AccuracyRadius : -1)
            : default;

    private static TLGeoPoint BuildGeoPoint(GeoSnapshot geo)
    {
        var builder = GeoPoint.Builder()
            .Lat(geo.Lat)
            .Longitude(geo.Longitude)
            .AccessHash(0);
        if (geo.AccuracyRadius >= 0)
        {
            builder = builder.AccuracyRadius(geo.AccuracyRadius);
        }
        return builder.Build();
    }

    private static byte[] BuildPhotoMedia(byte[] photoBytes, bool spoiler, int ttlSeconds)
    {
        var builder = MessageMediaPhoto.Builder().Photo(photoBytes);
        if (spoiler) builder = builder.Spoiler(true);
        if (ttlSeconds >= 0) builder = builder.TtlSeconds(ttlSeconds);
        using TLMessageMedia media = builder.Build();
        return media.AsSpan().ToArray();
    }

    private static byte[] BuildDocumentMedia(byte[] documentBytes, bool spoiler, int ttlSeconds)
    {
        var builder = MessageMediaDocument.Builder().Document(documentBytes);
        if (spoiler) builder = builder.Spoiler(true);
        if (ttlSeconds >= 0) builder = builder.TtlSeconds(ttlSeconds);
        using TLMessageMedia media = builder.Build();
        return media.AsSpan().ToArray();
    }

    private static async ValueTask<byte[]?> ProcessDocumentThumb(TLInputFile thumbFile,
        long documentId, IUploadService upload, IPhotoProcessingService photos,
        IUnitOfWork unitOfWork, IPhotoRepository photoRepository)
    {
        var saved = await upload.SaveFile(thumbFile);
        if (!saved.Success || saved.Result == null)
        {
            return null;
        }
        using TLUploadedFileInfo info = saved.Result.Value;
        var processed = await photos.ProcessPhoto(info);
        if (!processed.Success || processed.Result == null)
        {
            return null;
        }
        using TLPhoto photo = processed.Result.Value;
        long thumbPhotoId = photo.AsPhoto().Id;

        var thumbnailRows = photoRepository.GetThumbnails(thumbPhotoId);
        bool queued = true;
        foreach (var row in thumbnailRows)
        {
            using (row)
            {
                var thumbnail = (Thumbnail)row.AsSpan();
                using TLThumbnail rekeyed = Thumbnail.Builder()
                    .FileId(documentId)
                    .ThumbFileId(thumbnail.ThumbFileId)
                    .PhotoSize(thumbnail.PhotoSize)
                    .Build();
                queued = photoRepository.PutThumbnail((TLBytes)rekeyed) && queued;
            }
        }
        if (!queued || !await unitOfWork.SaveAsync())
        {
            return null;
        }
        return photo.AsPhoto().Sizes.ToReadOnlySpan().ToArray();
    }
}
