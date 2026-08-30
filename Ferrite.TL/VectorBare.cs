// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Runtime.InteropServices;

namespace Ferrite.TL;

public ref struct VectorBare
{
    private Span<byte> _buff;
    private int _position;
    private int _offset;
    public VectorBare()
    {
        _buff = new byte[512];
        SetCount(0);
        _position = 4;
        _offset = 4;
    }
    public VectorBare(Span<byte> buffer)
    {
        _offset = TryMeasure(buffer, out int measured)
            ? Math.Min(measured, buffer.Length)
            : buffer.Length;
        _buff = buffer[.._offset];
        _position = 4;
    }

    private static bool TryMeasure(Span<byte> data, out int length)
    {
        length = 4;
        if (data.Length < 4)
        {
            return false;
        }

        int count = MemoryMarshal.Read<int>(data[..4]);
        for (int i = 0; i < count; i++)
        {
            if (length + 4 > data.Length)
            {
                return false;
            }
            ObjectSizeReaderDelegate? sizeReader = ObjectReader.GetObjectSizeReader(
                MemoryMarshal.Read<int>(data.Slice(length, 4)));
            if (sizeReader == null)
            {
                return false;
            }
            length += sizeReader.Invoke(data, length);
        }
        return true;
    }
    public readonly int Constructor => 0;
    public ReadOnlySpan<byte> ToReadOnlySpan() => _buff[.._offset];
    public readonly int Count => MemoryMarshal.Read<int>(_buff);
    public readonly int Length => _offset;
    private void SetCount(int count)
    {
        MemoryMarshal.Write(_buff[..4], ref count);
    }

    public static Span<byte> Read(Span<byte> data, int offset)
    {
        int count = MemoryMarshal.Read<int>(data.Slice(offset, 4));
        int len = 4;
        for (int i = 0; i < count; i++)
        {
            var sizeReader = ObjectReader.GetObjectSizeReader(
                MemoryMarshal.Read<int>(data.Slice(offset + len, 4)));
            if (sizeReader != null) len += sizeReader.Invoke(data, offset + len);
        }
        return data.Slice(offset, len);
    }

    public static int ReadSize(Span<byte> data, int offset, ObjectSizeReaderDelegate? sizeReader = null)
    {
        int count = MemoryMarshal.Read<int>(data.Slice(offset, 4));
        int len = 4;
        for (int i = 0; i < count; i++)
        {
            var elementReader = sizeReader ?? ObjectReader.GetObjectSizeReader(
                MemoryMarshal.Read<int>(data.Slice(offset + len, 4)));
            if (elementReader != null) len += elementReader.Invoke(data, offset + len);
        }
        return len;
    }

    public void Append(ReadOnlySpan<byte> value)
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
        MemoryMarshal.Cast<byte, int>(_buff)[0]++;
        _offset += value.Length;
    }
    public ReadOnlySpan<byte> Read(ObjectReaderDelegate? reader = null)
    {
        if (_position == _offset)
        {
            throw new EndOfStreamException();
        }

        reader ??= ObjectReader.GetObjectReader(
            MemoryMarshal.Read<int>(_buff.Slice(_position, 4)));
        if (reader == null)
        {
            throw new NotSupportedException();
        }

        var result = reader.Invoke(_buff, _position);
        _position += result.Length;
        return result;
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
        _position = 4;
    }
}
