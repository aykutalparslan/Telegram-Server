// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Crypto;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using TLPhoto = Ferrite.TL.baseLayer.TLPhoto;

namespace Ferrite.Services;

public sealed class PhotoProcessingService : IPhotoProcessingService
{
    private readonly IFileInfoRepository _fileInfoRepository;
    private readonly IPhotoRepository _photoRepository;

    private readonly IObjectStore _objectStore;
    private readonly IPhotoProcessor _photoProcessor;
    private readonly IRandomGenerator _random;
    private readonly IUnitOfWork _unitOfWork;

    public PhotoProcessingService(IObjectStore objectStore,
        IPhotoProcessor photoProcessor, IRandomGenerator random, IUnitOfWork unitOfWork, IFileInfoRepository fileInfoRepository, IPhotoRepository photoRepository)
    {
        _fileInfoRepository = fileInfoRepository;
        _photoRepository = photoRepository;

        _objectStore = objectStore;
        _photoProcessor = photoProcessor;
        _random = random;
        _unitOfWork = unitOfWork;
    }

    public async ValueTask<ServiceResult<TLPhoto?>> ProcessPhoto(TLUploadedFileInfo file)
    {
        var info = file.AsUploadedFileInfo();
        var snapshot = new FileSnapshot(info.Id, info.Parts, info.AccessHash,
            info.FileReference.ToArray(), info.SavedOn, info.IsBigFile);
        var partRows = snapshot.IsBigFile
            ? _fileInfoRepository.GetBigFileParts(snapshot.Id)
            : _fileInfoRepository.GetFileParts(snapshot.Id);
        var parts = new List<PartSnapshot>(partRows.Count);
        long totalSize = 0;
        foreach (var row in partRows)
        {
            var part = row.AsFilePart();
            parts.Add(new PartSnapshot(part.PartNum, part.PartSize));
            totalSize += part.PartSize;
            row.Dispose();
        }
        parts.Sort((x, y) => x.Number.CompareTo(y.Number));
        if (parts.Count != snapshot.Parts || totalSize > UploadService.PhotoSizeLimit)
        {
            return new ServiceResult<TLPhoto?>(null, false,
                totalSize > UploadService.PhotoSizeLimit
                    ? ErrorMessages.PhotoFileTooBig
                    : ErrorMessages.PhotoFileInvalid);
        }
        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i].Number != i || parts[i].Size < 1)
            {
                return new ServiceResult<TLPhoto?>(null, false,
                    ErrorMessages.PhotoFileInvalid);
            }
        }

        var image = new byte[(int)totalSize];
        int offset = 0;
        foreach (var part in parts)
        {
            await using var stream = await (snapshot.IsBigFile
                ? _objectStore.GetBigFilePart(snapshot.Id, part.Number)
                : _objectStore.GetFilePart(snapshot.Id, part.Number));
            int remaining = part.Size;
            while (remaining > 0)
            {
                int read = await stream.ReadAsync(image.AsMemory(offset, remaining));
                if (read == 0)
                {
                    return new ServiceResult<TLPhoto?>(null, false,
                        ErrorMessages.PhotoFileInvalid);
                }
                offset += read;
                remaining -= read;
            }
        }

        var (width, height) = _photoProcessor.GetImageSize(image);
        if (width <= 0 || height <= 0)
        {
            return new ServiceResult<TLPhoto?>(null, false,
                ErrorMessages.PhotoFileInvalid);
        }

        var generated = new List<GeneratedThumbnail>();
        foreach (var spec in GetThumbnailSpecs(width, height))
        {
            byte[]? bytes = _photoProcessor.GenerateThumbnail(image, spec.MaxSize,
                spec.Filter);
            if (bytes == null || bytes.Length == 0)
            {
                return new ServiceResult<TLPhoto?>(null, false,
                    ErrorMessages.PhotoFileInvalid);
            }
            long thumbId = _random.NextLong();
            bool saved = await _objectStore.SaveFilePart(thumbId, 0,
                new MemoryStream(bytes));
            if (!saved)
            {
                return new ServiceResult<TLPhoto?>(null, false,
                    ErrorMessages.InternalServerError);
            }
            var dimensions = spec.Filter == ImageFilter.Crop
                ? (spec.MaxSize, spec.MaxSize)
                : ScaleToBox(width, height, spec.MaxSize);
            generated.Add(new GeneratedThumbnail(thumbId, spec.Type,
                dimensions.Item1, dimensions.Item2, bytes.Length));
        }

        var persisted = PersistProcessedPhoto(snapshot, generated);
        if (!persisted.Queued || !await _unitOfWork.SaveAsync())
        {
            persisted.Photo.Dispose();
            return new ServiceResult<TLPhoto?>(null, false,
                ErrorMessages.InternalServerError);
        }
        return new ServiceResult<TLPhoto?>(persisted.Photo, true, ErrorMessages.None);
    }

    private (TLPhoto Photo, bool Queued) PersistProcessedPhoto(FileSnapshot file,
        IReadOnlyCollection<GeneratedThumbnail> generated)
    {
        var sizes = new Vector();
        bool queued = true;
        long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        foreach (var item in generated)
        {
            using TLPhotoSize size = PhotoSize.Builder()
                .Type(Encoding.UTF8.GetBytes(item.Type))
                .W(item.Width).H(item.Height).Size(item.Size).Build();
            sizes.AppendTLObject(size.AsSpan());
            using TLThumbnail thumbnail = Thumbnail.Builder()
                .FileId(file.Id).ThumbFileId(item.FileId).PhotoSize(size.AsSpan()).Build();
            using TLFilePart part = new FilePart(item.FileId, 0, item.Size);
            using TLUploadedFileInfo info = UploadedFileInfo.Builder()
                .Id(item.FileId).PartSize(item.Size).Parts(1)
                .AccessHash(_random.NextLong())
                .Name(Encoding.UTF8.GetBytes(item.Type + ".jpg"))
                .SavedOn(now).IsBigFile(false)
                .FileType((int)StreamFileType.Jpeg).Build();
            queued = _fileInfoRepository.PutFilePart(part) && queued;
            queued = _fileInfoRepository.PutFileInfo(info) && queued;
            queued = _photoRepository.PutThumbnail((TLBytes)thumbnail) && queued;
        }

        int date = (int)DateTimeOffset.FromUnixTimeMilliseconds(file.SavedOn)
            .ToUnixTimeSeconds();
        TLPhoto photo = Photo.Builder()
            .Id(file.Id).AccessHash(file.AccessHash).FileReference(file.FileReference)
            .Date(date).Sizes(sizes).DcId(MediaDefaults.DcId).Build();
        queued = _photoRepository.PutPhoto((TLBytes)photo) && queued;
        return (photo, queued);
    }

    private static IReadOnlyCollection<ThumbnailSpec> GetThumbnailSpecs(int width,
        int height)
    {
        var result = new List<ThumbnailSpec>();
        if (width >= 160 && height >= 160) result.Add(new("a", 160, ImageFilter.Crop));
        else result.Add(new("s", 100, ImageFilter.Box));
        if (width >= 320 && height >= 320) result.Add(new("b", 320, ImageFilter.Crop));
        else if (width >= 320 || height >= 320) result.Add(new("m", 320, ImageFilter.Box));
        if (width >= 640 && height >= 640) result.Add(new("c", 640, ImageFilter.Crop));
        if (width >= 800 || height >= 800) result.Add(new("x", 800, ImageFilter.Box));
        if (width >= 1280 && height >= 1280) result.Add(new("d", 1280, ImageFilter.Crop));
        if (width >= 1280 || height >= 1280) result.Add(new("y", 1280, ImageFilter.Box));
        if (width >= 2560 || height >= 2560) result.Add(new("w", 2560, ImageFilter.Box));
        return result;
    }

    private static (int, int) ScaleToBox(int width, int height, int maxSize)
    {
        double scale = maxSize / (double)Math.Max(width, height);
        return (Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)));
    }

    private readonly record struct FileSnapshot(long Id, int Parts, long AccessHash,
        byte[] FileReference, long SavedOn, bool IsBigFile);
    private readonly record struct PartSnapshot(int Number, int Size);
    private readonly record struct ThumbnailSpec(string Type, int MaxSize,
        ImageFilter Filter);
    private readonly record struct GeneratedThumbnail(long FileId, string Type,
        int Width, int Height, int Size);
}
