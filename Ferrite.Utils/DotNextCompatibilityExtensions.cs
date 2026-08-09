// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Runtime.InteropServices;

namespace DotNext.Buffers
{
    public static class DotNextCompatibilityExtensions
    {
        public static int ReadInt32(this ref SequenceReader reader, bool littleEndian)
        {
            Span<byte> bytes = stackalloc byte[sizeof(int)];
            reader.Read(bytes);
            return littleEndian ? BinaryPrimitives.ReadInt32LittleEndian(bytes) : BinaryPrimitives.ReadInt32BigEndian(bytes);
        }

        public static long ReadInt64(this ref SequenceReader reader, bool littleEndian)
        {
            Span<byte> bytes = stackalloc byte[sizeof(long)];
            reader.Read(bytes);
            return littleEndian ? BinaryPrimitives.ReadInt64LittleEndian(bytes) : BinaryPrimitives.ReadInt64BigEndian(bytes);
        }

        public static double ReadDouble(this ref SequenceReader reader, bool littleEndian)
        {
            return BitConverter.Int64BitsToDouble(reader.ReadInt64(littleEndian));
        }

        public static T Read<T>(this ref SequenceReader reader) where T : unmanaged
        {
            Span<byte> bytes = stackalloc byte[Marshal.SizeOf<T>()];
            reader.Read(bytes);
            return MemoryMarshal.Read<T>(bytes);
        }

        public static int ReadInt32(this ref SpanReader<byte> reader, bool littleEndian)
        {
            var bytes = reader.Read(sizeof(int));
            return littleEndian ? BinaryPrimitives.ReadInt32LittleEndian(bytes) : BinaryPrimitives.ReadInt32BigEndian(bytes);
        }

        public static long ReadInt64(this ref SpanReader<byte> reader, bool littleEndian)
        {
            var bytes = reader.Read(sizeof(long));
            return littleEndian ? BinaryPrimitives.ReadInt64LittleEndian(bytes) : BinaryPrimitives.ReadInt64BigEndian(bytes);
        }

        public static void WriteInt32(this IBufferWriter<byte> writer, int value, bool littleEndian)
        {
            Span<byte> bytes = stackalloc byte[sizeof(int)];
            if (littleEndian)
            {
                BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            }
            else
            {
                BinaryPrimitives.WriteInt32BigEndian(bytes, value);
            }

            writer.Write(bytes);
        }

        public static void WriteInt32(this ref BufferWriterSlim<byte> writer, int value, bool littleEndian)
        {
            Span<byte> bytes = stackalloc byte[sizeof(int)];
            if (littleEndian)
            {
                BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            }
            else
            {
                BinaryPrimitives.WriteInt32BigEndian(bytes, value);
            }

            writer.Write(bytes);
        }

        public static void WriteInt64(this IBufferWriter<byte> writer, long value, bool littleEndian)
        {
            Span<byte> bytes = stackalloc byte[sizeof(long)];
            if (littleEndian)
            {
                BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
            }
            else
            {
                BinaryPrimitives.WriteInt64BigEndian(bytes, value);
            }

            writer.Write(bytes);
        }

        public static void Write<T>(this IBufferWriter<byte> writer, T value) where T : unmanaged
        {
            writer.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1)));
        }

        public static ReadOnlySequence<T> ToReadOnlySequence<T>(this SparseBufferWriter<T> writer)
        {
            var buffer = new T[checked((int)writer.WrittenCount)];
            writer.CopyTo(buffer);
            return new ReadOnlySequence<T>(buffer);
        }
    }
}

namespace DotNext.IO.Pipelines
{
    public static class DotNextPipelineCompatibilityExtensions
    {
        public static async ValueTask<int> ReadInt32Async(this PipeReader reader, bool littleEndian,
            CancellationToken cancellationToken = default)
        {
            var bytes = await ReadExactlyAsync(reader, sizeof(int), cancellationToken);
            return littleEndian ? BinaryPrimitives.ReadInt32LittleEndian(bytes) : BinaryPrimitives.ReadInt32BigEndian(bytes);
        }

        public static async ValueTask<long> ReadInt64Async(this PipeReader reader, bool littleEndian,
            CancellationToken cancellationToken = default)
        {
            var bytes = await ReadExactlyAsync(reader, sizeof(long), cancellationToken);
            return littleEndian ? BinaryPrimitives.ReadInt64LittleEndian(bytes) : BinaryPrimitives.ReadInt64BigEndian(bytes);
        }

        private static async ValueTask<byte[]> ReadExactlyAsync(PipeReader reader, int count,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;
                if (buffer.Length >= count)
                {
                    var bytes = new byte[count];
                    buffer.Slice(0, count).CopyTo(bytes);
                    reader.AdvanceTo(buffer.GetPosition(count));
                    return bytes;
                }

                if (result.IsCompleted)
                {
                    throw new EndOfStreamException();
                }

                reader.AdvanceTo(buffer.Start, buffer.End);
            }
        }
    }
}
