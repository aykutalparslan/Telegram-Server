// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using DotNext.Buffers;
using DotNext.IO;
using DotNext.IO.Pipelines;

namespace Ferrite.Utils;

public static class BufferExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> ToSpan(in this ReadOnlySequence<byte> buffer)
    {
        if (buffer.IsSingleSegment)
        {
            return buffer.FirstSpan;
        }
        return buffer.ToArray();
    }
    public static string ReadTLString(this ref SequenceReader reader)
    {
        return Encoding.UTF8.GetString(reader.ReadTLBytes());
    }
    public static Span<byte> ReadTLBytes(this ref SequenceReader reader)
    {
        int len;
        byte b = reader.ReadByte();
        len = b;
        int rem = (4 - ((len + 1) % 4)) % 4;
        if (len == 254)
        {
            Span<byte> len_bytes = stackalloc byte[3];
            reader.Read(len_bytes);
            len = ((int)len_bytes[0]) |
                ((int)len_bytes[1] << 8) |
                ((int)len_bytes[2] << 16);
            rem = (4 - ((len + 4) % 4)) % 4;
        }
        Span<byte> result = new byte[len];
        reader.Read(result);
        reader.Skip(rem);
        return result;
    }
    public static async Task<int> ReadTLBytesLength(this PipeReader reader)
    {
        var firstByte = new byte[1];
        await reader.ReadExactlyAsync(firstByte);
        byte b = firstByte[0];
        int len = b;
        if (len == 254)
        {
            var lenBytes = new byte[3];
            await reader.ReadExactlyAsync(lenBytes);
            len = ((int)lenBytes[0]) |
                  ((int)lenBytes[1] << 8) |
                  ((int)lenBytes[2] << 16);
        }
        return len;
    }
    public static async ValueTask ReadToMemoryAsync(this PipeReader reader, Memory<byte> destination,
        CancellationToken cancellationToken = default)
    {
        int copied = 0;
        while (copied < destination.Length)
        {
            var result = await reader.ReadAsync(cancellationToken);
            var buffer = result.Buffer;
            int toCopy = (int)Math.Min(buffer.Length, destination.Length - copied);
            if (toCopy > 0)
            {
                buffer.Slice(0, toCopy).CopyTo(destination.Span.Slice(copied, toCopy));
                copied += toCopy;
                reader.AdvanceTo(buffer.GetPosition(toCopy));
            }
            else
            {
                reader.AdvanceTo(buffer.Start, buffer.End);
            }

            if (copied == destination.Length)
            {
                return;
            }

            if (result.IsCompleted)
            {
                throw new EndOfStreamException();
            }
        }
    }
    public static void WriteTLString(this IBufferWriter<byte> writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.WriteTLBytes(bytes);
    }
    public static void WriteTLString(this SparseBufferWriter<byte> writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        ((IBufferWriter<byte>)writer).WriteTLBytes(bytes);
    }
    public static void WriteTLBytes(this SparseBufferWriter<byte> writer, in byte[] bytes)
    {
        ((IBufferWriter<byte>)writer).WriteTLBytes(in bytes);
    }
    public static void WriteTLBytes(this SparseBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        ((IBufferWriter<byte>)writer).WriteTLBytes(bytes);
    }
    public static void WriteTLBytes(this SparseBufferWriter<byte> writer, in ReadOnlySequence<byte> bytes, bool copymemory = true)
    {
        const byte zero = 0;
        int rem;
        if (bytes.Length < 254)
        {
            writer.Write((byte)bytes.Length);
            rem = (int)((4 - ((bytes.Length + 1) % 4)) % 4);
        }
        else
        {
            writer.Write((byte)254);
            writer.Write((byte)(bytes.Length & 0xff));
            writer.Write((byte)((bytes.Length >> 8) & 0xff));
            writer.Write((byte)((bytes.Length >> 16) & 0xff));
            rem = (int)((4 - ((bytes.Length + 4) % 4)) % 4);
        }
        writer.Write(bytes, copymemory);
        for (int i = 0; i < rem; i++)
        {
            writer.Write(zero);
        }
    }
    public static void WriteTLBytes(this IBufferWriter<byte> writer, in ReadOnlySequence<byte> bytes)
    {
        const byte zero = 0;
        int rem;
        if (bytes.Length < 254)
        {
            writer.Write((byte)bytes.Length);
            rem = (int)((4 - ((bytes.Length + 1) % 4)) % 4);
        }
        else
        {
            writer.Write((byte)254);
            writer.Write((byte)(bytes.Length & 0xff));
            writer.Write((byte)((bytes.Length >> 8) & 0xff));
            writer.Write((byte)((bytes.Length >> 16) & 0xff));
            rem = (int)((4 - ((bytes.Length + 4) % 4)) % 4);
        }
        writer.Write(bytes);
        for (int i = 0; i < rem; i++)
        {
            writer.Write(zero);
        }
    }
    public static void WriteTLBytes(this IBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        const byte zero = 0;
        int rem;
        if (bytes.Length < 254)
        {
            writer.Write((byte)bytes.Length);
            rem = (4 - ((bytes.Length + 1) % 4)) % 4;
        }
        else
        {
            writer.Write((byte)254);
            writer.Write((byte)(bytes.Length & 0xff));
            writer.Write((byte)((bytes.Length >> 8) & 0xff));
            writer.Write((byte)((bytes.Length >> 16) & 0xff));
            rem = (4 - ((bytes.Length + 4) % 4)) % 4;
        }
        writer.Write(bytes);
        for (int i = 0; i < rem; i++)
        {
            writer.Write(zero);
        }
    }
    public static void WriteTLBytes(this IBufferWriter<byte> writer, in byte[] bytes)
    {
        const byte zero = 0;
        int rem;
        if (bytes.Length < 254)
        {
            writer.Write((byte)bytes.Length);
            rem = (4 - ((bytes.Length + 1) % 4)) % 4;
        }
        else
        {
            writer.Write((byte)254);
            writer.Write((byte)(bytes.Length & 0xff));
            writer.Write((byte)((bytes.Length >> 8) & 0xff));
            writer.Write((byte)((bytes.Length >> 16) & 0xff));
            rem = (4 - ((bytes.Length + 4) % 4)) % 4;
        }
        writer.Write(bytes);
        for (int i = 0; i < rem; i++)
        {
            writer.Write(zero);
        }
    }
    /*public static void WriteTLBytes(this ref SpanWriter<byte> writer, in byte[] bytes)
    {
        const byte zero = 0;
        int rem;
        if (bytes.Length < 254)
        {
            writer.Write((byte)bytes.Length);
            rem = (4 - ((bytes.Length + 1) % 4)) % 4;
        }
        else
        {
            writer.Write((byte)254);
            writer.Write((byte)(bytes.Length & 0xff));
            writer.Write((byte)((bytes.Length >> 8) & 0xff));
            writer.Write((byte)((bytes.Length >> 16) & 0xff));
            rem = (4 - ((bytes.Length + 4) % 4)) % 4;
        }
        writer.Write(bytes);
        for (int i = 0; i < rem; i++)
        {
            writer.Write(zero);
        }
    }*/
}
