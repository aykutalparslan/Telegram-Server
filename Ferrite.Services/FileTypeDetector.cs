// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services;

// Detects the storage.FileType of an uploaded file from the magic bytes of
// its first part, per the documented storage.FileType semantics
// (https://core.telegram.org/api/files). The server only needs the broad
// content class; anything unrecognized is served as fileUnknown.
public static class FileTypeDetector
{
    public static StreamFileType Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return StreamFileType.Jpeg;
        }
        if (header.Length >= 8 && header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return StreamFileType.Png;
        }
        if (header.Length >= 4 && header.StartsWith("GIF8"u8))
        {
            return StreamFileType.Gif;
        }
        if (header.Length >= 12 && header.StartsWith("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return StreamFileType.Webp;
        }
        if (header.Length >= 12 && header.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            return header.Slice(8, 4).SequenceEqual("qt  "u8) ? StreamFileType.Mov : StreamFileType.Mp4;
        }
        if (header.Length >= 8 && (header.Slice(4, 4).SequenceEqual("moov"u8) ||
                                   header.Slice(4, 4).SequenceEqual("mdat"u8) ||
                                   header.Slice(4, 4).SequenceEqual("wide"u8) ||
                                   header.Slice(4, 4).SequenceEqual("free"u8)))
        {
            return StreamFileType.Mov;
        }
        if (header.Length >= 3 && header.StartsWith("ID3"u8))
        {
            return StreamFileType.Mp3;
        }
        // MP3 frame sync: eleven set bits at the start of the first frame.
        if (header.Length >= 2 && header[0] == 0xFF && (header[1] & 0xE0) == 0xE0)
        {
            return StreamFileType.Mp3;
        }
        return StreamFileType.Unknown;
    }
}
