// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using DotNext;
using SkiaSharp;

namespace Ferrite.Services;

public class SkiaPhotoProcessor : IPhotoProcessor
{
    private static readonly SKSamplingOptions ThumbnailSampling = new(SKCubicResampler.Mitchell);

    public byte[]? GenerateThumbnail(ReadOnlySpan<byte> src, int w, ImageFilter type)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(src);
            if (type == ImageFilter.Crop)
            {
                var width = bitmap.Width;
                var height = bitmap.Height;
                var size = Math.Min(width, height);
                var x = (width - size) / 2;
                var y = (height - size) / 2;
                var rect = new SKRectI(x, y, x + size, y + size);
                using var cropped = new SKBitmap(size, size);
                bitmap.ExtractSubset(cropped, rect);
                using var scaled = new SKBitmap(w, w);
                cropped.ScalePixels(scaled, ThumbnailSampling);
                using var data = scaled.Encode(SKEncodedImageFormat.Jpeg, 65);
                return data.ToArray();
            }
            else
            {
                double scale = w / (double)Math.Max(bitmap.Width, bitmap.Height);
                int targetWidth = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
                int targetHeight = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
                using var scaled = new SKBitmap(targetWidth, targetHeight);
                bitmap.ScalePixels(scaled, ThumbnailSampling);
                using var data = scaled.Encode(SKEncodedImageFormat.Jpeg, 65);
                return data.ToArray();
            }
        }
        catch (Exception)
        {
            return null;
        }
        
    }
    public (int w, int h) GetImageSize(ReadOnlySpan<byte> src)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(src);
            return (bitmap.Width, bitmap.Height);
        }
        catch (Exception)
        {
            return (0, 0);
        }
    }
}
