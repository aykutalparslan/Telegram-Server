// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Runtime.InteropServices;
using Ferrite.Utils;

namespace Ferrite.TL;

// A tde2e bare vector of TL bytes: an int32 count followed by count TL-encoded
// byte strings, with no 1cb5c415 constructor. Backs e2e.chain.sharedKey's
// dest_header, which the reference serializes with
// TlStoreVector<TlStoreString>.
public ref struct VectorBareOfString
{
    private Span<byte> _buff;
    private int _offset;

    public VectorBareOfString()
    {
        _buff = new byte[512];
        SetCount(0);
        _offset = 4;
    }

    public VectorBareOfString(Span<byte> buffer)
    {
        _buff = buffer;
        _offset = ReadSize(buffer, 0);
    }

    public readonly int Constructor => 0;
    public readonly int Count => _buff.Length >= 4 ? MemoryMarshal.Read<int>(_buff[..4]) : 0;
    public readonly int Length => _offset;
    public readonly ReadOnlySpan<byte> ToReadOnlySpan() => _buff[.._offset];

    public readonly ReadOnlySpan<byte> this[int index]
    {
        get
        {
            int position = 4;
            for (int i = 0; i < index; i++)
            {
                position += BufferUtils.GetTLBytesLength(_buff, position);
            }
            return BufferUtils.GetTLBytes(_buff, position);
        }
    }

    private void SetCount(int count)
    {
        MemoryMarshal.Write(_buff[..4], ref count);
    }

    public void Append(ReadOnlySpan<byte> value)
    {
        int required = _offset + BufferUtils.CalculateTLBytesLength(value.Length);
        if (required > _buff.Length)
        {
            int newLength = Math.Max(_buff.Length * 2, required);
            var tmp = new byte[newLength];
            _buff.CopyTo(tmp);
            _buff = tmp;
        }
        int lenBytes = BufferUtils.WriteLenBytes(_buff, value, _offset);
        value.CopyTo(_buff[(_offset + lenBytes)..]);
        _offset += BufferUtils.CalculateTLBytesLength(value.Length);
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
        int len = 4;
        for (int i = 0; i < count; i++)
        {
            len += BufferUtils.GetTLBytesLength(data, offset + len);
        }
        if (offset + len > data.Length) throw new InvalidOperationException();
        return len;
    }
}
