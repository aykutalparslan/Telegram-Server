// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ferrite.Utils;

namespace Ferrite.TL;

public ref struct VectorOfString
{
    private Span<byte> _buff;
    private int _position;
    private int _offset;
    public VectorOfString()
    {
        _buff = new byte[512];
        SetConstructor(unchecked((int)0x1cb5c415));
        SetCount(0);
        _position = 8;
        _offset = 8;
    }
    public VectorOfString(Span<byte> buffer)
    {
        if (MemoryMarshal.Read<int>(buffer) != unchecked((int)0x1cb5c415))
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
        if (MemoryMarshal.Read<int>(data[..4]) != unchecked((int)0x1cb5c415))
        {
            throw new InvalidOperationException();
        }
        int count = MemoryMarshal.Read<int>(data.Slice(offset + 4, 4));
        int len = 8;
        for (int i = 0; i < count; i++)
        {
            len += BufferUtils.GetTLBytesLength(data, offset + len);
        }
        return data.Slice(offset, len);
    }

    public static int ReadSize(Span<byte> data, int offset)
    {
        if (MemoryMarshal.Read<int>(data[offset..]) != unchecked((int)0x1cb5c415))
        {
            throw new InvalidOperationException();
        }
        int count = MemoryMarshal.Read<int>(data.Slice(offset + 4, 4));
        int len = 8;
        for (int i = 0; i < count; i++)
        {
            len += BufferUtils.GetTLBytesLength(data, offset + len);
        }
        return len;
    }

    public void AppendTLBytes(ReadOnlySpan<byte> value)
    {
        int len = BufferUtils.CalculateTLBytesLength(value.Length);
        if (value.Length + len + _offset > _buff.Length)
        {
            var tmp = new byte[_buff.Length * 2];
            _buff.CopyTo(tmp);
            _buff = tmp;
        }
        int lenBytes = BufferUtils.WriteLenBytes(_buff, value, _offset);
        value.CopyTo(_buff[(lenBytes + _offset)..]);
        MemoryMarshal.Cast<byte, int>(_buff)[1]++;
        _offset += len;
    }

    public Span<byte> ReadTLBytes()
    {
        if (_position == _offset)
        {
            throw new EndOfStreamException();
        }
        int bytesLength = BufferUtils.GetTLBytesLength(_buff, _position);
        var result = BufferUtils.GetTLBytes(_buff, _position);
        _position += bytesLength;
        return result;
    }
    public void Reset()
    {
        _position = 8;
    }
}