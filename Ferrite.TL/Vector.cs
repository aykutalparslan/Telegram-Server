// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ferrite.Utils;

namespace Ferrite.TL;

public ref struct Vector
{
    public const int ConstructorId = unchecked((int)0x1cb5c415);

    private Span<byte> _buff;
    private int _position;
    private int _offset;
    public Vector()
    {
        _buff = new byte[512];
        SetConstructor(ConstructorId);
        SetCount(0);
        _position = 8;
        _offset = 8;
    }
    public Vector(Span<byte> buffer)
    {
        if (MemoryMarshal.Read<int>(buffer) != ConstructorId)
        {
            throw new InvalidOperationException();
        }
        _offset = Math.Min(ReadSize(buffer, 0), buffer.Length);
        _buff = buffer[.._offset];
        _position = 8;
    }
    public readonly int Constructor => MemoryMarshal.Read<int>(_buff);
    private void SetConstructor(int constructor)
    {
        MemoryMarshal.Write(_buff[..4], ref constructor);
    }
    public ReadOnlySpan<byte> ToReadOnlySpan() => _buff[.._offset];
    public readonly int Count => MemoryMarshal.Read<int>(_buff.Slice(4, 4));
    public readonly int Length => _offset;
    private void SetCount(int count)
    {
        MemoryMarshal.Write(_buff.Slice(4, 4), ref count);
    }

    public static Span<byte> Read(Span<byte> data, int offset)
    {
        if (MemoryMarshal.Read<int>(data[..4]) != ConstructorId)
        {
            throw new InvalidOperationException();
        }
        int count = MemoryMarshal.Read<int>(data.Slice(offset + 4, 4));
        int len = 8;
        for (int i = 0; i < count; i++)
        {
            var sizeReader = ObjectReader.GetObjectSizeReader(
                MemoryMarshal.Read<int>(data.Slice(offset + len, 4)));
            if (sizeReader != null) len += sizeReader.Invoke(data, len);
        }
        return data.Slice(offset, len);
    }

    public static int ReadSize(Span<byte> data, int offset)
    {
        if (MemoryMarshal.Read<int>(data[offset..]) != ConstructorId)
        {
            throw new InvalidOperationException();
        }
        int count = MemoryMarshal.Read<int>(data.Slice(offset + 4, 4));
        int len = 8;
        for (int i = 0; i < count; i++)
        {
            var sizeReader = ObjectReader.GetObjectSizeReader(
                MemoryMarshal.Read<int>(data.Slice(offset + len, 4)));
            if (sizeReader != null) len += sizeReader.Invoke(data, offset + len);
        }
        return len;
    }

    public void AppendTLObject(ReadOnlySpan<byte> value)
    {
        if (value.Length + _offset > _buff.Length)
        {
            int newLength = _buff.Length * 2;
            while (value.Length + _offset > newLength)
            {
                newLength *= 2;
            }
            var tmp = new byte[newLength];
            _buff.CopyTo(tmp);
            _buff = tmp;
        }
        value.CopyTo(_buff[_offset..]);
        MemoryMarshal.Cast<byte, int>(_buff)[1]++;
        _offset += value.Length;
    }
    public Span<byte> ReadTLObject()
    {
        if (_position == _offset)
        {
            throw new EndOfStreamException();
        }
        ObjectReaderDelegate? reader = ObjectReader.GetObjectReader(
            MemoryMarshal.Read<int>(_buff.Slice(_position, 4)));
        if (reader == null)
        {
            throw new NotSupportedException();
        }

        var result = reader.Invoke(_buff, _position);
        _position += result.Length;
        return result;
    }
    public void Reset()
    {
        _position = 8;
    }
}
