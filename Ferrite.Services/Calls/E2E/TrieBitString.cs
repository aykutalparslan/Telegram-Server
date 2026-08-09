// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers.Binary;

namespace Ferrite.Services.Calls.E2E;

// Raised when tde2e-encoded input cannot be decoded, or when a change carries a
// key the reference would reject outright.
public sealed class ChainCodecException : Exception
{
    public ChainCodecException(string message) : base(message)
    {
    }
}

// A growable little-endian writer for the tde2e trie encodings.
//
// These encodings are NOT TL constructors: they are the reference's own
// store_for_hash / store_for_network / store_for_snapshot layouts
// They are the one deliberate non-TL codec in the server, and the byte layout
// must match the client's exactly.
public sealed class TrieByteWriter
{
    private byte[] _buffer = new byte[256];

    public int Position { get; private set; }

    private void Ensure(int extra)
    {
        if (Position + extra <= _buffer.Length) return;
        int length = _buffer.Length;
        while (length < Position + extra) length *= 2;
        Array.Resize(ref _buffer, length);
    }

    public void WriteByte(byte value)
    {
        Ensure(1);
        _buffer[Position++] = value;
    }

    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        Ensure(value.Length);
        value.CopyTo(_buffer.AsSpan(Position));
        Position += value.Length;
    }

    public void WriteInt32(int value)
    {
        Ensure(4);
        BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(Position), value);
        Position += 4;
    }

    public void WriteInt64(long value)
    {
        Ensure(8);
        BinaryPrimitives.WriteInt64LittleEndian(_buffer.AsSpan(Position), value);
        Position += 8;
    }

    public void WriteInt64At(int position, long value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(_buffer.AsSpan(position), value);
    }

    // td::store(const string&) — the ordinary TL bytes encoding: a short form
    // below 254, a 0xFE long form above it, then zero padding to 4 bytes.
    public void WriteTlString(ReadOnlySpan<byte> value)
    {
        int written;
        if (value.Length < 254)
        {
            WriteByte((byte)value.Length);
            written = 1 + value.Length;
        }
        else
        {
            WriteByte(0xFE);
            WriteByte((byte)value.Length);
            WriteByte((byte)(value.Length >> 8));
            WriteByte((byte)(value.Length >> 16));
            written = 4 + value.Length;
        }
        WriteBytes(value);
        while (written % 4 != 0)
        {
            WriteByte(0);
            written++;
        }
    }

    public byte[] ToArray() => _buffer.AsSpan(0, Position).ToArray();
}

// The reader half of TrieByteWriter.
public ref struct TrieByteReader
{
    private readonly ReadOnlySpan<byte> _buffer;

    public TrieByteReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
        Position = 0;
    }

    public int Position { get; private set; }
    public readonly bool AtEnd => Position >= _buffer.Length;

    private void Require(int count)
    {
        if (Position + count > _buffer.Length)
        {
            throw new ChainCodecException("truncated trie encoding");
        }
    }

    public byte ReadByte()
    {
        Require(1);
        return _buffer[Position++];
    }

    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        Require(count);
        var slice = _buffer.Slice(Position, count);
        Position += count;
        return slice;
    }

    public int ReadInt32()
    {
        Require(4);
        int value = BinaryPrimitives.ReadInt32LittleEndian(_buffer[Position..]);
        Position += 4;
        return value;
    }

    public long ReadInt64()
    {
        Require(8);
        long value = BinaryPrimitives.ReadInt64LittleEndian(_buffer[Position..]);
        Position += 8;
        return value;
    }

    public byte[] ReadTlString()
    {
        byte first = ReadByte();
        int length;
        int read;
        if (first < 254)
        {
            length = first;
            read = 1 + length;
        }
        else
        {
            length = ReadByte() | (ReadByte() << 8) | (ReadByte() << 16);
            read = 4 + length;
        }
        if (length < 0) throw new ChainCodecException("negative string length");
        byte[] value = ReadBytes(length).ToArray();
        while (read % 4 != 0)
        {
            ReadByte();
            read++;
        }
        return value;
    }
}

// A big-endian bit slice over a shared byte buffer, ported from the tde2e
// reference BitString. The reference keeps a byte pointer plus begin/end bit
// offsets and indexes backwards past it; this port keeps the owning array and
// one absolute begin-bit, which is equivalent and avoids negative indexing.
//
// Store below is byte-exact with BitString.cpp:233-267. It feeds the trie node
// hash preimage, so a wrong mask here produces proofs the client cannot build
// on, with no local symptom at all.
public readonly struct TrieBitString
{
    private readonly byte[]? _data;
    private readonly int _beginBit;

    public TrieBitString(byte[]? data, int beginBit, int bitLength)
    {
        _data = data;
        _beginBit = beginBit;
        BitLength = bitLength;
    }

    public int BitLength { get; }
    public bool HasData => _data != null;

    // Proof key material is padded/truncated to 32 bytes, mirroring to_key().
    public static TrieBitString FromKey(ReadOnlySpan<byte> key)
    {
        var buffer = new byte[32];
        key[..Math.Min(32, key.Length)].CopyTo(buffer);
        return new TrieBitString(buffer, 0, 256);
    }

    public static TrieBitString Allocate(int beginBit, int bitLength)
    {
        int end = beginBit + bitLength;
        var buffer = new byte[end / 8 + 2];
        return new TrieBitString(buffer, beginBit, bitLength);
    }

    private byte[] Data => _data ?? throw new ChainCodecException("bit string has no buffer");

    private int EndBitAbsolute => _beginBit + BitLength;
    private int BeginByte => (_beginBit + 7) / 8;
    private int EndByte => EndBitAbsolute / 8;
    private int BytesSize => EndByte - BeginByte;
    // Only the within-byte offset is observable in the encoding, which is why
    // the reference can hand a sibling slice a fresh buffer starting at this
    // value and still produce identical bytes.
    internal int BeginBitInByte => _beginBit % 8;
    private int EndBitInByte => EndBitAbsolute % 8;

    private static byte BeginMask(int start) => (byte)(0xFF >> start);
    private static byte EndMask(int end) => (byte)(0xFF << (8 - end));
    private static byte CreateMask(int start, int end) => (byte)(BeginMask(start) & EndMask(end));

    public byte GetBit(int position)
    {
        int absolute = _beginBit + position;
        return (byte)((Data[absolute / 8] >> (7 - absolute % 8)) & 1);
    }

    public TrieBitString Substr(int position, int length = int.MaxValue)
    {
        int remaining = BitLength - position;
        int newLength = length < remaining ? length : remaining;
        if (newLength < 0) newLength = 0;
        return new TrieBitString(_data, _beginBit + position, newLength);
    }

    public int CommonPrefixLength(TrieBitString other)
    {
        int limit = Math.Min(BitLength, other.BitLength);
        for (int i = 0; i < limit; i++)
        {
            if (GetBit(i) != other.GetBit(i)) return i;
        }
        return limit;
    }

    public bool ValueEquals(TrieBitString other)
    {
        if (BitLength != other.BitLength) return false;
        return CommonPrefixLength(other) == BitLength;
    }

    public void Store(TrieByteWriter writer)
    {
        writer.WriteInt32(unchecked((int)(((uint)BeginBitInByte << 16) |
            (uint)(BeginBitInByte + BitLength))));

        var data = Data;
        int written = 0;
        if (BytesSize == -1)
        {
            writer.WriteByte((byte)(data[BeginByte - 1] &
                CreateMask(BeginBitInByte, EndBitInByte)));
            written = 1;
        }
        else
        {
            if (BeginBitInByte != 0)
            {
                writer.WriteByte((byte)(data[BeginByte - 1] & BeginMask(BeginBitInByte)));
                written++;
            }
            writer.WriteBytes(data.AsSpan(BeginByte, BytesSize));
            written += BytesSize;
            if (EndBitInByte != 0)
            {
                writer.WriteByte((byte)(data[EndByte] & EndMask(EndBitInByte)));
                written++;
            }
        }
        while (written % 4 != 0)
        {
            writer.WriteByte(0);
            written++;
        }
    }

    // Inverse of Store. `baseString` is the caller's running key buffer, so
    // sibling slices share storage exactly as fetch_bit_string does: a child's
    // bits are written into the parent's buffer at the parent's offset, which
    // is what makes a reconstructed leaf's key_suffix hash identically.
    public static TrieBitString Fetch(ref TrieByteReader reader, TrieBitString baseString)
    {
        uint header = unchecked((uint)reader.ReadInt32());
        int begin = (int)(header >> 16);
        int end = (int)(header & 0xFFFF);
        int length = end - begin;
        if (length < 0) throw new ChainCodecException("negative bit-string length");

        TrieBitString result = baseString.HasData
            ? baseString.Substr(0, length)
            : Allocate(begin, length);

        var data = result.Data;
        int read = 0;
        if (result.BytesSize == -1)
        {
            byte value = reader.ReadByte();
            data[result.BeginByte - 1] |=
                (byte)(value & CreateMask(result.BeginBitInByte, result.EndBitInByte));
            read = 1;
        }
        else
        {
            if (result.BeginBitInByte != 0)
            {
                byte value = reader.ReadByte();
                data[result.BeginByte - 1] |= (byte)(value & BeginMask(result.BeginBitInByte));
                read++;
            }
            reader.ReadBytes(result.BytesSize).CopyTo(data.AsSpan(result.BeginByte));
            read += result.BytesSize;
            if (result.EndBitInByte != 0)
            {
                byte value = reader.ReadByte();
                data[result.EndByte] |= (byte)(value & EndMask(result.EndBitInByte));
                read++;
            }
        }
        while (read % 4 != 0)
        {
            reader.ReadByte();
            read++;
        }
        return result;
    }
}
