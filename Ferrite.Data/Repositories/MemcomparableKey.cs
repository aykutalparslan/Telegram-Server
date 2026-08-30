// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;
using Ferrite.Data.Repositories;
using xxHash;

namespace Ferrite.Data.Repositories;

// encodes multi column keys to a single key using the memcomparable format
// here: https://github.com/facebook/mysql-5.6/wiki/MyRocks-record-format#memcomparable-format
// documentation license: Creative Commons Attribution 4.0 International Public License
// Some implementation details from:
// https://github.com/facebook/mysql-5.6/blob/fb-mysql-5.6.35/sql/field.cc
// Licensed under:
/*
   Copyright (c) 2000, 2016, Oracle and/or its affiliates. All rights reserved.
   This program is free software; you can redistribute it and/or modify
   it under the terms of the GNU General Public License as published by
   the Free Software Foundation; version 2 of the License.
   This program is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
   GNU General Public License for more details.
   You should have received a copy of the GNU General Public License
   along with this program; if not, write to the Free Software
   Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA 02110-1301  USA
*/
// Some implementation details from:
// https://github.com/facebook/mysql-5.6/blob/fb-mysql-5.6.35/storage/rocksdb/rdb_datadic.cc
// Licensed under:
/*
   Copyright (c) 2012,2013 Monty Program Ab
   This program is free software; you can redistribute it and/or modify
   it under the terms of the GNU General Public License as published by
   the Free Software Foundation; version 2 of the License.
   This program is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
   GNU General Public License for more details.
   You should have received a copy of the GNU General Public License
   along with this program; if not, write to the Free Software
   Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA */

public class MemcomparableKey
{
    private readonly byte[] _key;
    public ReadOnlySpan<byte> Value => _key;
    public Span<byte> Span => _key;
    public byte[] ArrayValue => _key;
    public long ExpiresAt { get; set; }
    private MemcomparableKey(byte[] value)
    {
        _key = value;
    }
    public MemcomparableKey(string tableName)
    {
        var chars = tableName.AsSpan();
        var bytes = MemoryMarshal.Cast<char, byte>(chars);
        var hash = bytes.GetXxHash64();
        _key = new byte[8];
        Encode(_key, (long)hash, 0);
    }
    
    public MemcomparableKey(string tableName, int value)
    {
        var chars = tableName.AsSpan();
        var bytes = MemoryMarshal.Cast<char, byte>(chars);
        var hash = bytes.GetXxHash64();
        _key = new byte[12];
        Encode(_key, (long)hash, 0);
        Encode(_key, value, 8);
    }
    public MemcomparableKey(string tableName, bool value)
    {
        var chars = tableName.AsSpan();
        var bytes = MemoryMarshal.Cast<char, byte>(chars);
        var hash = bytes.GetXxHash64();
        byte byteValue = (byte)(value ? 1 : 0);
        _key = new byte[9];
        Encode(_key, (long)hash, 0);
        _key[8] |= byteValue;
    }
    public MemcomparableKey(string tableName, long value)
    {
        var chars = tableName.AsSpan();
        var bytes = MemoryMarshal.Cast<char, byte>(chars);
        var hash = bytes.GetXxHash64();
        _key = new byte[16];
        Encode(_key, (long)hash, 0);
        Encode(_key, value, 8);
    }
    public MemcomparableKey(string tableName, DateTime value)
    {
        long val = value.Ticks;
        var chars = tableName.AsSpan();
        var bytes = MemoryMarshal.Cast<char, byte>(chars);
        var hash = bytes.GetXxHash64();
        _key = new byte[16];
        Encode(_key, (long)hash, 0);
        Encode(_key, val, 8);
    }
    public MemcomparableKey(string tableName, DateTimeOffset value)
    {
        long val = value.Ticks;
        var chars = tableName.AsSpan();
        var bytes = MemoryMarshal.Cast<char, byte>(chars);
        var hash = bytes.GetXxHash64();
        _key = new byte[16];
        Encode(_key, (long)hash, 0);
        Encode(_key, val, 8);
    }
    public MemcomparableKey(string tableName, float value)
    {
        var chars = tableName.AsSpan();
        var bytes = MemoryMarshal.Cast<char, byte>(chars);
        var hash = bytes.GetXxHash64();
        _key = new byte[12];
        Encode(_key, (long)hash, 0);
        Encode(_key, value, 8);
    }
    public MemcomparableKey(string tableName, double value)
    {
        var chars = tableName.AsSpan();
        var bytes = MemoryMarshal.Cast<char, byte>(chars);
        var hash = bytes.GetXxHash64();
        _key = new byte[16];
        Encode(_key, (long)hash, 0);
        Encode(_key, value, 8);
    }
    public MemcomparableKey(string tableName, Span<byte> value)
    {
        var newKey = new MemcomparableKey(tableName).Append(value);
        _key = newKey._key;
    }

    public MemcomparableKey(string tableName, string val)
    {
        var value = Encoding.UTF8.GetBytes(val).AsSpan();
        var newKey = new MemcomparableKey(tableName).Append(value);
        _key = newKey._key;
    }
    public static MemcomparableKey From(byte[] value)
    {
        return new MemcomparableKey(value);
    }
    public MemcomparableKey Append(Span<byte> value)
    {
        int blocks = value.Length / 8 + 
            (value.Length % 8 == 0 ? 0 : 1);
        byte[] newKey = new byte[_key.Length + (blocks > 0 ? blocks * 9 : 9)];
        _key.CopyTo(newKey.AsSpan());
        for (int i = 0; i < blocks; i++)
        {
            int significantBytes = value.Length - i * 8;
            if (significantBytes > 8)
            {
                significantBytes = 9;
            }
            newKey[_key.Length + i * 9 + 8] = (byte)significantBytes;
            value.Slice(i * 8, Math.Min(significantBytes, 8))
                .CopyTo(newKey.AsSpan(_key.Length + i * 9));
        }

        return new MemcomparableKey(newKey);
    }
    public MemcomparableKey Append(bool value)
    {
        byte[] newKey = new byte[_key.Length + 1];
        _key.CopyTo(newKey.AsSpan());
        byte byteValue = (byte)(value ? 1 : 0);
        newKey[_key.Length] = byteValue;

        return new MemcomparableKey(newKey);
    }
    public MemcomparableKey Append(int value)
    {
        byte[] newKey = new byte[_key.Length + 4];
        _key.CopyTo(newKey.AsSpan());
        Encode(newKey, value, _key.Length);
        return new MemcomparableKey(newKey);
    }
    public MemcomparableKey Append(long value)
    {
        byte[] newKey = new byte[_key.Length + 8];
        _key.CopyTo(newKey.AsSpan());
        Encode(newKey, value, _key.Length);
        return new MemcomparableKey(newKey);
    }
    public MemcomparableKey Append(float value)
    {
        byte[] newKey = new byte[_key.Length + 4];
        _key.CopyTo(newKey.AsSpan());
        BinaryPrimitives.WriteSingleBigEndian(newKey.AsSpan().Slice(_key.Length), value);
        Encode(newKey, value, _key.Length);
        return new MemcomparableKey(newKey);
    }
    public MemcomparableKey Append(double value)
    {
        byte[] newKey = new byte[_key.Length + 8];
        _key.CopyTo(newKey.AsSpan());
        BinaryPrimitives.WriteDoubleBigEndian(newKey.AsSpan().Slice(_key.Length), value);
        Encode(newKey, value, _key.Length);
        return new MemcomparableKey(newKey);
    }
    public MemcomparableKey Append(string value)
    {
        return Append(Encoding.UTF8.GetBytes(value));
    }
    public MemcomparableKey Append(DateTime value)
    {
        return Append(value.Ticks);
    }
    public MemcomparableKey Append(DateTimeOffset value)
    {
        return Append(value.Ticks);
    }
    public static MemcomparableKey Create(string tableName, IReadOnlyCollection<object> values)
    {
        MemcomparableKey key = new MemcomparableKey(tableName);
        foreach (var v in values)
        {
            if (v is int intValue)
            {
                key = key.Append(intValue);
            }
            else if (v is bool boolValue)
            {
                key = key.Append(boolValue);
            }
            else if (v is long longValue)
            {
                key = key.Append(longValue);
            }
            else if (v is float floatValue)
            {
                key = key.Append(floatValue);
            }
            else if (v is double doubleValue)
            {
                key = key.Append(doubleValue);
            }
            else if (v is string stringValue)
            {
                key = key.Append(stringValue);
            }
            else if (v is DateTime dateTimeValue)
            {
                key = key.Append(dateTimeValue);
            }
            else if (v is DateTimeOffset dateTimeOffsetValue)
            {
                key = key.Append(dateTimeOffsetValue);
            }
            else if (v is byte[] bytesValue)
            {
                key = key.Append(bytesValue);
            }
        }

        return key;
    }

    public object? GetValue(KeyDefinition definition, string name)
    {
        int index = definition.GetOrdinal(name);
        if (definition[index].Type == DataType.Int)
        {
            return GetInt32(definition, definition[index].Name);
        }
        else if (definition[index].Type == DataType.Bool)
        {
            return GetBool(definition, definition[index].Name);
        }
        else if (definition[index].Type == DataType.Long)
        {
            return GetInt64(definition, definition[index].Name);
        }
        else if (definition[index].Type == DataType.Float)
        {
            return GetSingle(definition, definition[index].Name);
        }
        else if (definition[index].Type == DataType.Double)
        {
            return GetDouble(definition, definition[index].Name);
        }
        else if (definition[index].Type == DataType.String)
        {
            return GetString(definition, definition[index].Name);
        }
        else if (definition[index].Type == DataType.DateTime)
        {
            return GetDateTime(definition, definition[index].Name);
        }
        else if (definition[index].Type == DataType.Bytes)
        {
            return GetBytes(definition, definition[index].Name);
        }

        return null;
    }

    public bool? GetBool(KeyDefinition definition, string field)
    {
        int offset = 8;
        if (definition.HasColumn(field))
        {
            foreach (var col in definition.Columns)
            {
                if(col.Name == field) break;
                offset = CalculateOffset(col, offset);
            }

            return _key[offset] == 1;
        }

        return null;
    }
    public int? GetInt32(KeyDefinition definition, string field)
    {
        int offset = 8;
        if (definition.HasColumn(field))
        {
            foreach (var col in definition.Columns)
            {
                if(col.Name == field) break;
                offset = CalculateOffset(col, offset);
            }

            return DecodeInt32(_key, offset);
        }

        return null;
    }
    public long? GetInt64(KeyDefinition definition, string field)
    {
        int offset = 8;
        if (definition.HasColumn(field))
        {
            foreach (var col in definition.Columns)
            {
                if(col.Name == field) break;
                offset = CalculateOffset(col, offset);
            }

            return DecodeInt64(_key, offset);
        }

        return null;
    }
    public float? GetSingle(KeyDefinition definition, string field)
    {
        int offset = 8;
        if (definition.HasColumn(field))
        {
            foreach (var col in definition.Columns)
            {
                if(col.Name == field) break;
                offset = CalculateOffset(col, offset);
            }

            return DecodeSingle(_key, offset);
        }

        return null;
    }
    public double? GetDouble(KeyDefinition definition, string field)
    {
        int offset = 8;
        if (definition.HasColumn(field))
        {
            foreach (var col in definition.Columns)
            {
                if(col.Name == field) break;
                offset = CalculateOffset(col, offset);
            }

            return DecodeDouble(_key, offset);
        }

        return null;
    }
    public DateTime? GetDateTime(KeyDefinition definition, string field)
    {
        int offset = 8;
        if (definition.HasColumn(field))
        {
            foreach (var col in definition.Columns)
            {
                if(col.Name == field) break;
                offset = CalculateOffset(col, offset);
            }

            new DateTime(DecodeInt64(_key, offset));
        }

        return null;
    }
    public DateTimeOffset? GetDateTimeOffset(KeyDefinition definition, string field)
    {
        int offset = 8;
        if (definition.HasColumn(field))
        {
            foreach (var col in definition.Columns)
            {
                if(col.Name == field) break;
                offset = CalculateOffset(col, offset);
            }

            return new DateTimeOffset(new DateTime(DecodeInt64(_key, offset)));
        }

        return null;
    }
    public byte[]? GetBytes(KeyDefinition definition, string field)
    {
        int offset = 8;
        if (definition.HasColumn(field))
        {
            foreach (var col in definition.Columns)
            {
                if(col.Name == field) break;
                offset = CalculateOffset(col, offset);
            }

            return DecodeBytes(_key, offset);
        }

        return null;
    }
    public string? GetString(KeyDefinition definition, string field)
    {
        int offset = 8;
        if (definition.HasColumn(field))
        {
            foreach (var col in definition.Columns)
            {
                if(col.Name == field) break;
                offset = CalculateOffset(col, offset);
            }

            var bytes = DecodeBytes(_key, offset);
            return Encoding.UTF8.GetString(bytes);
        }

        return null;
    }

    private int CalculateOffset(DataColumn col, int offset)
    {
        if (col.Type == DataType.Bool) offset += 1;
        else if (col.Type == DataType.Int) offset += 4;
        else if (col.Type == DataType.Long) offset += 8;
        else if (col.Type == DataType.Float) offset += 4;
        else if (col.Type == DataType.Double) offset += 8;
        else if (col.Type == DataType.DateTime) offset += 8;
        else if (col.Type == DataType.Bytes)
        {
            GetBytesLen(_key, offset, out var blocks);
            offset += (blocks > 0 ? blocks * 9 : 9);
        }
        else if (col.Type == DataType.String)
        {
            GetBytesLen(_key, offset, out var blocks);
            offset += (blocks > 0 ? blocks * 9 : 9);
        }
        return offset;
    }

    private static void Encode(byte[] key, int value, int offset)
    {
        if (key.Length - offset < 4) 
            throw new ArgumentOutOfRangeException(nameof(offset));
        BinaryPrimitives.WriteInt32BigEndian(key.AsSpan(offset), value);
        if (value < 0)
        {
            for (int i = 0; i < 4; i++)
            {
                key[offset + i] = (byte)(~key[offset + i]);
            }
        }
        else
        {
            key[offset] ^= 128;
        }
    }
    private static int DecodeInt32(byte[] key, int offset)
    {
        if (key.Length - offset < 4) 
            throw new ArgumentOutOfRangeException(nameof(offset));
        Span<byte> val = stackalloc byte[4];
        key.AsSpan(offset, 4).CopyTo(val);
        if ((val[0] & 128) != 0)
        {
            val[0] ^= 128;
        }
        else
        {
            for (int i = 0; i < 4; i++)
            {
                val[i] = (byte)(~val[i]);
            }
        }

        return BinaryPrimitives.ReadInt32BigEndian(val);
    }
    private static void Encode(byte[] key, long value, int offset)
    {
        if (key.Length - offset < 4) throw new ArgumentOutOfRangeException();
        BinaryPrimitives.WriteInt64BigEndian(key.AsSpan(offset), value);
        if (value < 0)
        {
            for (int i = 0; i < 8; i++)
            {
                key[offset + i] = (byte)(~key[offset + i]);
            }
        }
        else
        {
            key[offset] ^= 128;
        }
    }
    private static long DecodeInt64(byte[] key, int offset)
    {
        if (key.Length - offset < 8) 
            throw new ArgumentOutOfRangeException(nameof(offset));
        Span<byte> val = stackalloc byte[8];
        key.AsSpan(offset, 8).CopyTo(val);
        if ((val[0] & 128) != 0)
        {
            val[0] ^= 128;
        }
        else
        {
            for (int i = 0; i < 8; i++)
            {
                val[i] = (byte)(~val[i]);
            }
        }

        return BinaryPrimitives.ReadInt64BigEndian(val);
    }
    private static void Encode(byte[] key, float value, int offset)
    {
        if (key.Length - offset < 4) 
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (value == 0.0f)
        {
            key[offset] = 128;
            for (int i = 1; i < 4; i++)
            {
                key[offset + i] = 0;
            }
            return;
        }
        BinaryPrimitives.WriteSingleBigEndian(key.AsSpan(offset), value);
        if (value < 0)
        {
            for (int i = 0; i < 4; i++)
            {
                key[offset + i] = (byte)(~key[offset + i]);
            }
        }
        else
        {
            ushort exponent = (ushort)((key[offset] << 8) | key[offset + 1]);
            exponent |= 32768;
            exponent += 1 << 7;
            key[offset] = (byte)(exponent >> 8);
            key[offset + 1] = (byte)(exponent);
        }
    }
    private static float DecodeSingle(byte[] key, int offset)
    {
        if (key.Length - offset < 4) 
            throw new ArgumentOutOfRangeException(nameof(offset));
        Span<byte> val = stackalloc byte[4];
        key.AsSpan(offset, 4).CopyTo(val);
        if ((val[0] & 128) != 0)
        {
            if (val[1] == 0 && val[2] == 0 && val[3] == 0) return 0.0f;
            ushort exponent = (ushort)((val[0] << 8) | val[1]);
            exponent ^= 32768;
            exponent -= 1 << 7;
            val[0] = (byte)(exponent >> 8);
            val[1] = (byte)exponent;
        }
        else
        {
            for (int i = 0; i < 4; i++)
            {
                val[i] = (byte)(~val[i]);
            }
        }

        return BinaryPrimitives.ReadSingleBigEndian(val);
    }
    private static void Encode(byte[] key, double value, int offset)
    {
        if (key.Length - offset < 8) 
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (value == 0.0d)
        {
            key[offset] = 128;
            for (int i = 1; i < 8; i++)
            {
                key[offset + i] = 0;
            }
            return;
        }
        BinaryPrimitives.WriteDoubleBigEndian(key.AsSpan(offset), value);
        if (value < 0)
        {
            for (int i = 0; i < 8; i++)
            {
                key[offset + i] = (byte)(~key[offset + i]);
            }
        }
        else
        {
            ushort exponent = (ushort)((key[offset] << 8) | key[offset + 1]);
            exponent |= 32768;
            exponent += 1 << 4;
            key[offset] = (byte)(exponent >> 8);
            key[offset + 1] = (byte)(exponent);
        }
    }
    private static double DecodeDouble(byte[] key, int offset)
    {
        if (key.Length - offset < 8) 
            throw new ArgumentOutOfRangeException(nameof(offset));
        Span<byte> val = stackalloc byte[8];
        key.AsSpan(offset, 8).CopyTo(val);
        if ((val[0] & 128) != 0)
        {
            if (val[1] == 0 && val[2] == 0 && val[3] == 0 && val[4] == 0 && 
                val[5] == 0 && val[6] == 0 && val[7] == 0) return 0.0f;
            ushort exponent = (ushort)((val[0] << 8) | val[1]);
            exponent ^= 32768;
            exponent -= 1 << 4;
            val[0] = (byte)(exponent >> 8);
            val[1] = (byte)exponent;
        }
        else
        {
            for (int i = 0; i < 8; i++)
            {
                val[i] = (byte)(~val[i]);
            }
        }

        return BinaryPrimitives.ReadDoubleBigEndian(val);
    }
    private static byte[] DecodeBytes(byte[] key, int offset)
    {
        if (key.Length - offset < 9) 
            throw new ArgumentOutOfRangeException(nameof(offset));
        var len = GetBytesLen(key, offset, out var blocks);

        if (len == 0) return Array.Empty<byte>();
        var result = new byte[len];
        for (int i = 0; i < blocks; i++)
        {
            int toBeCopied = Math.Min(8, len);
            key.AsSpan(offset + i * 9, toBeCopied)
                .CopyTo(result.AsSpan(i * 8));
            len -= toBeCopied;
        }

        return result;
    }

    private static int GetBytesLen(byte[] key, int offset, out int blocks)
    {
        int len = 0;
        blocks = 0;
        do
        {
            len += key[offset + (blocks * 9) + 8] == 9 ? 8 : key[offset + (blocks * 9) + 8];
        } while (key[offset + (blocks++ * 9) + 8] == 9);

        return len;
    }
}
