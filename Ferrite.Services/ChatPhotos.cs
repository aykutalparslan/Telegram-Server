// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using TLPhoto = Ferrite.TL.baseLayer.TLPhoto;

namespace Ferrite.Services;

// Shared chat/channel photo helpers. Resolving an InputChatPhoto edit request
// reuses the profile-photo pipeline: an inputChatUploadedPhoto is finalized and
// processed (thumbnail ladder + canonical photo# row) exactly like a profile
// photo, and an inputChatPhoto re-selects an existing stored photo. The compact
// chat/channel row only carries the small chatPhoto# object (photo_id + dc_id);
// the full photo# row lives in the photos store and is looked up by that id.
public static class ChatPhotos
{
    // Outcome of resolving an InputChatPhoto edit request.
    public readonly record struct ChatPhotoResolution(
        bool IsDelete, long PhotoId, byte[]? PhotoBytes, ErrorMessage? Error)
    {
        public static readonly ChatPhotoResolution Delete = new(true, 0, null, null);
        public static ChatPhotoResolution Set(long photoId, byte[] photoBytes) =>
            new(false, photoId, photoBytes, null);
        public static ChatPhotoResolution Fail(ErrorMessage error) =>
            new(false, 0, null, error);
    }

    // Resolves the serialized InputChatPhoto bytes into a delete, a stored photo
    // (id + full wire photo# bytes), or a typed error. Ref-struct views are read
    // synchronously into heap values before any await.
    public static async ValueTask<ChatPhotoResolution> ResolveAsync(byte[] inputChatPhotoBytes,
        IUploadService upload, IPhotoProcessingService photos,
        IPhotoRepository photoRepository)
    {
        TLInputFile? uploaded = null;
        long existingId = 0;
        long existingHash = 0;
        byte[] existingReference = Array.Empty<byte>();
        var view = (InputChatPhotoView)inputChatPhotoBytes.AsSpan();
        if (view.Is(out InputChatPhotoEmpty _))
        {
            return ChatPhotoResolution.Delete;
        }
        if (view.Is(out InputChatUploadedPhoto up) && up.Flags[0] &&
            TryReadInputFile(up.Get_FileView(), out var dto))
        {
            uploaded = dto;
        }
        else if (view.Is(out InputChatPhoto existing) &&
                 existing.Get_IdView().Is(out InputPhoto id))
        {
            existingId = id.Id;
            existingHash = id.AccessHash;
            existingReference = id.FileReference.ToArray();
        }
        else
        {
            return ChatPhotoResolution.Fail(ErrorMessages.PhotoInvalid);
        }

        if (uploaded != null)
        {
            ServiceResult<TLUploadedFileInfo?> saved;
            using (TLInputFile input = uploaded.Value)
            {
                saved = await upload.SaveFile(input);
            }
            if (!saved.Success || saved.Result == null)
            {
                return ChatPhotoResolution.Fail(saved.ErrorMessage);
            }
            using TLUploadedFileInfo info = saved.Result.Value;
            var processed = await photos.ProcessPhoto(info);
            if (!processed.Success || processed.Result == null)
            {
                return ChatPhotoResolution.Fail(processed.ErrorMessage);
            }
            using TLPhoto photo = processed.Result.Value;
            return ChatPhotoResolution.Set(photo.AsPhoto().Id, photo.AsSpan().ToArray());
        }

        using TLBytes? canonical = photoRepository.GetPhoto(existingId);
        if (canonical == null)
        {
            return ChatPhotoResolution.Fail(ErrorMessages.PhotoInvalid);
        }
        var stored = new Photo(canonical.Value.AsSpan());
        if (stored.Constructor != Constructors.baseLayer_Photo ||
            stored.AccessHash != existingHash ||
            !stored.FileReference.SequenceEqual(existingReference))
        {
            return ChatPhotoResolution.Fail(ErrorMessages.PhotoInvalid);
        }
        return ChatPhotoResolution.Set(stored.Id, canonical.Value.AsSpan().ToArray());
    }

    // Reads the compact-row chatPhoto# photo id (null for chatPhotoEmpty).
    public static long? ReadPhotoId(ChatPhotoView photo) =>
        photo.Is(out ChatPhoto value) ? value.PhotoId : null;

    // Builds the small compact-row chatPhoto# object (photo_id + dc_id).
    public static byte[] BuildCompactChatPhoto(long photoId)
    {
        using var chatPhoto = ChatPhoto.Builder()
            .PhotoId(photoId)
            .DcId(MediaDefaults.DcId)
            .Build();
        return chatPhoto.ToReadOnlySpan().ToArray();
    }

    // Takes an owned generated copy because the input view may be span-backed.
    public static bool TryReadInputFile(InputFileView input, out TLInputFile value)
    {
        if (input.Is(out InputFile file))
        {
            InputFile owned = InputFile.Builder()
                .Id(file.Id).Parts(file.Parts).Name(file.Name)
                .Md5Checksum(file.Md5Checksum).Build();
            value = owned;
            return true;
        }
        if (input.Is(out InputFileBig big))
        {
            InputFileBig owned = InputFileBig.Builder()
                .Id(big.Id).Parts(big.Parts).Name(big.Name).Build();
            value = owned;
            return true;
        }
        value = default;
        return false;
    }
}
