// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Runtime.InteropServices;

namespace Ferrite.TL;

// A tde2e bare vector of longs: an int32 count followed by count int64 values,
// with no 1cb5c415 constructor. The reference serializes
// e2e.chain.sharedKey's dest_user_id with TlStoreVector, which writes only the
// count, so a boxed Vector here would corrupt every block hash.
public ref struct VectorBareOfLong
{
    private Span<byte> _buff;
    private int _offset;

    public VectorBareOfLong()
    {
        _buff = new byte[4 + 32 * 8];
        SetCount(0);
        _offset = 4;
    }

    public VectorBareOfLong(Span<byte> buffer)
    {
        _buff = buffer;
        _offset = ReadSize(buffer, 0);
    }

    public readonly int Constructor => 0;
    public readonly int Count => _buff.Length >= 4 ? MemoryMarshal.Read<int>(_buff[..4]) : 0;
    public readonly int Length => _offset;
    public readonly ReadOnlySpan<byte> ToReadOnlySpan() => _buff[.._offset];

    public readonly long this[int index] =>
        MemoryMarshal.Read<long>(_buff.Slice(4 + index * 8, 8));

    private void SetCount(int count)
    {
        MemoryMarshal.Write(_buff[..4], ref count);
    }

    public void Append(long value)
    {
        if (_offset + 8 > _buff.Length)
        {
            int newLength = Math.Max(_buff.Length * 2, _offset + 8);
            var tmp = new byte[newLength];
            _buff.CopyTo(tmp);
            _buff = tmp;
        }
        MemoryMarshal.Write(_buff.Slice(_offset, 8), ref value);
        _offset += 8;
        SetCount(Count + 1);
    }

    public static Span<byte> Read(Span<byte> data, int offset)
    {
        return data.Slice(offset, ReadSize(data, offset));
    }

    public static int ReadSize(Span<byte> data, int offset)
    {
        if (offset + 4 > data.Length) return 0;
        int count = MemoryMarshal.Read<int>(data.Slice(offset, 4));
        if (count < 0) throw new InvalidOperationException();
        int len = 4 + count * 8;
        if (offset + len > data.Length) throw new InvalidOperationException();
        return len;
    }
}
