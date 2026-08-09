// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ferrite.Utils;

public class BufferUtils
{
    public static int CalculateTLBytesLength(in int length)
    {
        int result = 0;
        if (length < 254)
        {
            result = length + 1 + (4 - (length+ 1) % 4) % 4;
        }
        else
        {
            result = length + 4 + (4 - length % 4) % 4;   
        }
        return result;
    }
    public static int WriteLenBytes(Span<byte> buffer, ReadOnlySpan<byte> value, int offset)
    {
        var lenBytes = 0;
        if (value.Length < 254)
        {
            lenBytes++;
            byte len = (byte)value.Length;
            MemoryMarshal.Write(buffer[offset..], ref len);
        }
        else
        {
            byte b = 254;
            MemoryMarshal.Write(buffer[offset..], ref b);
            b = (byte)(value.Length & 0xff);
            MemoryMarshal.Write(buffer[(offset+1)..], ref b);
            b = (byte)((value.Length >> 8) & 0xff);
            MemoryMarshal.Write(buffer[(offset+2)..], ref b);
            b = (byte)((value.Length >> 16) & 0xff);
            MemoryMarshal.Write(buffer[(offset+3)..], ref b);
            lenBytes += 4;
        }

        return lenBytes;
    }
    /// <summary>
    /// Decodes the size of a TL serialized bare string including the padding bytes.
    /// </summary>
    /// <param name="buffer">Source buffer.</param>
    /// <param name="offset">Offset in the source buffer.</param>
    /// <returns></returns>
    public static int GetTLBytesLength(Span<byte> buffer, int offset)
    {
        if (buffer.Length - offset < 4) return 0;
        int len = buffer[offset];
        var rem = (4 - ((len + 1) % 4)) % 4;
        if (len != 254) return len + rem + 1;
        len = (buffer[offset + 1] & 0xff) |
              ((buffer[offset + 2] & 0xff) << 8) |
              ((buffer[offset + 3] & 0xff) << 16);
        rem = (4 - len % 4) % 4;
        return len + 4 + rem;
    }
    /// <summary>
    /// Decodes the value of a TL serialized bare string.
    /// </summary>
    /// <param name="buffer">Source buffer.</param>
    /// <param name="offset">Offset in the source buffer.</param>
    /// <returns></returns>
    public static unsafe Span<byte> GetTLBytes(Span<byte> buffer, int offset)
    {
        if (buffer.Length - offset < 4) return new Span<byte>();
        int len = buffer[offset];
        if (offset + len > buffer.Length) return new Span<byte>();
        if (len != 254) return buffer.Slice(offset + 1, len);
        len = (buffer[offset + 1] & 0xff) |
              ((buffer[offset + 2] & 0xff) << 8) |
              ((buffer[offset + 3] & 0xff) << 16);
        return offset + len > buffer.Length ? new Span<byte>() : 
            buffer.Slice(offset + 4, len);
    }
}