// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ferrite.TL;

public ref struct VectorOfInt
{
    private Span<int> _buff;
    private int _offset;
    public VectorOfInt()
    {
        _buff = new int[32];
        SetConstructor(unchecked((int)0x1cb5c415));
        SetCount(0);
        _offset = 2;
    }
    public VectorOfInt(Span<byte> buffer)
    {
        if (MemoryMarshal.Read<int>(buffer[..4]) != unchecked((int)0x1cb5c415))
        {
            throw new InvalidOperationException();
        }
        _buff = MemoryMarshal.Cast<byte,int>(buffer);
        _offset = 2;
    }
    public readonly int Constructor => _buff[0];
    private void SetConstructor(int constructor)
    {
        _buff[0] = constructor;
    }
    public ReadOnlySpan<byte> ToReadOnlySpan() => MemoryMarshal.Cast<int, byte>(_buff)[..Length];
    public readonly int Count => _buff[1];
    public readonly int Length => Count * 4 + 8;
    private void SetCount(int count)
    {
        _buff[1] = count;
    }

    public static Span<byte> Read(Span<byte> data, int offset)
    {
        if (MemoryMarshal.Read<int>(data.Slice(offset,4)) != unchecked((int)0x1cb5c415))
        {
            throw new InvalidOperationException();
        }
        int count = MemoryMarshal.Read<int>(data.Slice(offset + 4, 4));
        int len = 8 + count * 4;
        if (offset + len > data.Length)
        {
            throw new InvalidOperationException();
        }
        return data.Slice(offset, len);
    }

    public static int ReadSize(Span<byte> data, int offset)
    {
        if (MemoryMarshal.Read<int>(data.Slice(offset,4)) != unchecked((int)0x1cb5c415))
        {
            throw new InvalidOperationException();
        }
        int count = MemoryMarshal.Read<int>(data.Slice(offset + 4, 4));
        return 8 + count * 4;
    }

    public void Append(int value)
    {
        if (_buff.Length == _offset)
        {
            var tmp = new int[_buff.Length * 2];
            _buff.CopyTo(tmp);
            _buff = tmp;
        }
        _buff[1]++;
        _buff[_offset++] = value;
    }
    public ref readonly int this[int index] => ref _buff[2 + index];

    public int[] ToArray()
    {
        int count = Count;
        var arr = new int[count];
        for (int i = 0; i < count; i++)
        {
            arr[i] = this[i];
        }

        return arr;
    }
}