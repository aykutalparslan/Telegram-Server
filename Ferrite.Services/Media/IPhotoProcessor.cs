// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using DotNext;

namespace Ferrite.Services.Media;

public interface IPhotoProcessor
{
    public byte[]? GenerateThumbnail(ReadOnlySpan<byte> src, int w, ImageFilter type);
    public (int w, int h) GetImageSize(ReadOnlySpan<byte> src);
}