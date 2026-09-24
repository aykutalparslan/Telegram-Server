// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.IO;

namespace Ferrite.TL.Schema;

internal static class LayerWireCodec
{
    internal static LayerObjectValue ReadBoxedObject(LayerWireReader reader,
        LayerSchema schema)
    {
        int id = reader.ReadInt32();
        if (!schema.TryGetConstructor(id, out LayerConstructor constructor))
        {
            throw new LayerUncoveredException("Unknown source constructor 0x" +
                                              unchecked((uint)id).ToString("x8") + ".");
        }
        return ReadObjectBody(reader, schema, constructor);
    }

    internal static LayerObjectValue ReadObjectBody(LayerWireReader reader,
        LayerSchema schema, LayerConstructor constructor)
    {
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (LayerField field in constructor.Fields)
        {
            if (field.IsConditional)
            {
                if (!fields.TryGetValue(field.FlagsField!, out object? flagsValue) ||
                    flagsValue is not int flags)
                {
                    throw new LayerWireException("Conditional field references missing flags.");
                }
                if ((flags & (1 << field.FlagBit!.Value)) == 0)
                {
                    continue;
                }
                if (field.Type.Kind == LayerWireKind.True)
                {
                    fields.Add(field.Name, true);
                    continue;
                }
            }
            fields.Add(field.Name, ReadValue(reader, schema, field.Type));
        }
        return new LayerObjectValue(constructor, fields);
    }

    internal static object ReadValue(LayerWireReader reader, LayerSchema schema,
        LayerType type)
    {
        switch (type.Kind)
        {
            case LayerWireKind.Int32:
            case LayerWireKind.Flags:
                return reader.ReadInt32();
            case LayerWireKind.Int64:
                return reader.ReadInt64();
            case LayerWireKind.Double:
                return reader.ReadDouble();
            case LayerWireKind.Int128:
                return reader.ReadFixed(16);
            case LayerWireKind.Int256:
                return reader.ReadFixed(32);
            case LayerWireKind.Int512:
                return reader.ReadFixed(64);
            case LayerWireKind.Bytes:
            case LayerWireKind.String:
                return reader.ReadTlBytes();
            case LayerWireKind.True:
                return true;
            case LayerWireKind.Object:
                if (type.IsBare)
                {
                    if (type.Name == null ||
                        !schema.TryGetBareConstructor(type.Name,
                            out LayerConstructor bareConstructor))
                    {
                        throw new LayerUncoveredException("Bare type is ambiguous: " +
                                                          type.Name + ".");
                    }
                    return ReadObjectBody(reader, schema, bareConstructor);
                }
                LayerObjectValue boxed = ReadBoxedObject(reader, schema);
                if (type.Name != null &&
                    !string.Equals(type.Name, boxed.Constructor.ResultType,
                        StringComparison.Ordinal))
                {
                    throw new LayerWireException("Boxed object has the wrong result type.");
                }
                return boxed;
            case LayerWireKind.Vector:
                if (!type.IsBare)
                {
                    int vectorId = reader.ReadInt32();
                    if (vectorId != LayerSchemaReader.VectorConstructorId)
                    {
                        throw new LayerWireException("Invalid vector constructor.");
                    }
                }
                int count = reader.ReadInt32();
                if (count < 0 || count > 1_000_000)
                {
                    throw new LayerWireException("Invalid vector count.");
                }
                var values = new object?[count];
                for (int i = 0; i < count; i++)
                {
                    values[i] = ReadValue(reader, schema, type.ElementType!);
                }
                return Array.AsReadOnly(values);
            default:
                throw new LayerWireException("Unknown wire type.");
        }
    }

    internal static void WriteBoxedObject(LayerWireWriter writer,
        LayerSchema schema, LayerObjectValue value)
    {
        LayerConstructor constructor = schema.GetConstructor(value.Constructor.Name);
        writer.WriteInt32(constructor.Id);
        WriteObjectBody(writer, schema, constructor, value);
    }

    internal static void WriteObjectBody(LayerWireWriter writer,
        LayerSchema schema, LayerConstructor constructor, LayerObjectValue value)
    {
        var flags = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (LayerField field in constructor.Fields)
        {
            if (field.IsConditional && value.Fields.ContainsKey(field.Name))
            {
                flags.TryGetValue(field.FlagsField!, out int current);
                flags[field.FlagsField!] = current | (1 << field.FlagBit!.Value);
            }
        }

        foreach (LayerField field in constructor.Fields)
        {
            if (field.Type.Kind == LayerWireKind.Flags)
            {
                flags.TryGetValue(field.Name, out int flagValue);
                writer.WriteInt32(flagValue);
                continue;
            }
            if (!value.TryGetValue(field.Name, out object? fieldValue))
            {
                if (field.IsConditional)
                {
                    continue;
                }
                throw new LayerUncoveredException("Required output field is absent: " +
                                                  field.Name + ".");
            }
            if (field.IsConditional && field.Type.Kind == LayerWireKind.True)
            {
                continue;
            }
            WriteValue(writer, schema, field.Type, fieldValue);
        }
    }

    internal static void WriteValue(LayerWireWriter writer, LayerSchema schema,
        LayerType type, object? value)
    {
        if (value == null)
        {
            throw new LayerUncoveredException("Output value is null.");
        }
        switch (type.Kind)
        {
            case LayerWireKind.Int32:
            case LayerWireKind.Flags:
                writer.WriteInt32((int)value);
                return;
            case LayerWireKind.Int64:
                writer.WriteInt64((long)value);
                return;
            case LayerWireKind.Double:
                writer.WriteDouble((double)value);
                return;
            case LayerWireKind.Int128:
            case LayerWireKind.Int256:
            case LayerWireKind.Int512:
                writer.Write((byte[])value);
                return;
            case LayerWireKind.Bytes:
            case LayerWireKind.String:
                writer.WriteTlBytes((byte[])value);
                return;
            case LayerWireKind.True:
                return;
            case LayerWireKind.Object:
                if (type.IsBare)
                {
                    LayerObjectValue bare = (LayerObjectValue)value;
                    LayerConstructor constructor = schema.GetConstructor(bare.Constructor.Name);
                    if (type.Name != null &&
                        !string.Equals(type.Name, constructor.ResultType,
                            StringComparison.Ordinal))
                    {
                        throw new LayerUncoveredException("Bare object has the wrong result type.");
                    }
                    WriteObjectBody(writer, schema, constructor, bare);
                    return;
                }
                LayerObjectValue boxed = (LayerObjectValue)value;
                LayerConstructor boxedConstructor = schema.GetConstructor(
                    boxed.Constructor.Name);
                if (type.Name != null &&
                    !string.Equals(type.Name, boxedConstructor.ResultType,
                        StringComparison.Ordinal))
                {
                    throw new LayerUncoveredException("Boxed object has the wrong result type.");
                }
                WriteBoxedObject(writer, schema, boxed);
                return;
            case LayerWireKind.Vector:
                if (!type.IsBare)
                {
                    writer.WriteInt32(LayerSchemaReader.VectorConstructorId);
                }
                var values = (IReadOnlyList<object?>)value;
                writer.WriteInt32(values.Count);
                for (int i = 0; i < values.Count; i++)
                {
                    WriteValue(writer, schema, type.ElementType!, values[i]);
                }
                return;
            default:
                throw new LayerWireException("Unknown output wire type.");
        }
    }
}

internal sealed class LayerWireReader
{
    private readonly byte[] _buffer;
    private int _offset;

    public bool IsComplete => _offset == _buffer.Length;

    public LayerWireReader(byte[] buffer)
    {
        _buffer = buffer;
    }

    public int ReadInt32()
    {
        Require(4);
        int value = _buffer[_offset] | _buffer[_offset + 1] << 8 |
                    _buffer[_offset + 2] << 16 | _buffer[_offset + 3] << 24;
        _offset += 4;
        return value;
    }

    public long ReadInt64()
    {
        Require(8);
        ulong value = 0;
        for (int i = 0; i < 8; i++)
        {
            value |= (ulong)_buffer[_offset + i] << (8 * i);
        }
        _offset += 8;
        return unchecked((long)value);
    }

    public double ReadDouble()
    {
        long bits = ReadInt64();
        return BitConverter.Int64BitsToDouble(bits);
    }

    public byte[] ReadFixed(int length)
    {
        Require(length);
        var value = new byte[length];
        Buffer.BlockCopy(_buffer, _offset, value, 0, length);
        _offset += length;
        return value;
    }

    public byte[] ReadTlBytes()
    {
        Require(1);
        int first = _buffer[_offset++];
        int length;
        int headerLength;
        if (first < 254)
        {
            length = first;
            headerLength = 1;
        }
        else if (first == 254)
        {
            Require(3);
            length = _buffer[_offset] | _buffer[_offset + 1] << 8 |
                     _buffer[_offset + 2] << 16;
            _offset += 3;
            headerLength = 4;
        }
        else
        {
            throw new LayerWireException("Invalid TL bytes length prefix.");
        }

        byte[] value = ReadFixed(length);
        int padding = (4 - ((headerLength + length) % 4)) % 4;
        Require(padding);
        _offset += padding;
        return value;
    }

    private void Require(int count)
    {
        if (count < 0 || _offset > _buffer.Length - count)
        {
            throw new LayerWireException("TL payload is truncated.");
        }
    }
}

internal sealed class LayerWireWriter
{
    private readonly MemoryStream _stream = new MemoryStream();

    public void WriteInt32(int value)
    {
        Write(new[]
        {
            (byte)value,
            (byte)(value >> 8),
            (byte)(value >> 16),
            (byte)(value >> 24)
        });
    }

    public void WriteInt64(long value)
    {
        ulong bits = unchecked((ulong)value);
        var bytes = new byte[8];
        for (int i = 0; i < 8; i++)
        {
            bytes[i] = (byte)(bits >> (8 * i));
        }
        Write(bytes);
    }

    public void WriteDouble(double value)
    {
        WriteInt64(BitConverter.DoubleToInt64Bits(value));
    }

    public void WriteTlBytes(byte[] value)
    {
        int headerLength;
        if (value.Length < 254)
        {
            Write(new[] { (byte)value.Length });
            headerLength = 1;
        }
        else
        {
            if (value.Length > 0x00ffffff)
            {
                throw new LayerWireException("TL bytes value is too long.");
            }
            Write(new[]
            {
                (byte)254,
                (byte)value.Length,
                (byte)(value.Length >> 8),
                (byte)(value.Length >> 16)
            });
            headerLength = 4;
        }
        Write(value);
        int padding = (4 - ((headerLength + value.Length) % 4)) % 4;
        if (padding != 0)
        {
            Write(new byte[padding]);
        }
    }

    public void Write(byte[] value)
    {
        _stream.Write(value, 0, value.Length);
    }

    public byte[] ToArray()
    {
        return _stream.ToArray();
    }
}

internal sealed class LayerWireException : Exception
{
    public LayerWireException(string message) : base(message)
    {
    }
}

internal sealed class LayerUncoveredException : Exception
{
    public LayerUncoveredException(string message) : base(message)
    {
    }
}
