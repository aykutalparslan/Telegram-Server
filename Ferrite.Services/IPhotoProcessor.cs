// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using DotNext;

namespace Ferrite.Services;

public interface IPhotoProcessor
{
    /// <summary>
    /// Generates a thumbnail of the photo.
    /// </summary>
    /// <param name="src"></param>
    /// <param name="w"></param>
    /// <param name="type"></param>
    /// <returns>Generated thumbnail or null if the operation fails.</returns>
    public byte[]? GenerateThumbnail(ReadOnlySpan<byte> src, int w, ImageFilter type);
    /// <summary>
    /// Gets the dimensions of the image.
    /// </summary>
    /// <param name="src"></param>
    /// <returns>Width and height of the image or (0,0) if the image file is invalid.</returns>
    public (int w, int h) GetImageSize(ReadOnlySpan<byte> src);
}