// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;

namespace Ferrite.Crypto;

public class IncrementalCrc32
{
    static readonly uint[] Table = new uint[256];
    static IncrementalCrc32()
    {
        Table[0] = 0;
        uint crc;
        for(uint i = 0; i < 256; i++)
        {
            crc = i;
            for (uint j = 0; j < 8; j++)
            {
                var tmp = crc & 1;
                crc = tmp == 1 ? 0xEDB88320 ^ (crc >> 1) : (crc >> 1);
                Table[i] = crc;
            }
        }
    }

    private uint _crc32 = 0xFFFFFFFFu;
    private uint _index = 0;
    public uint Crc32 => _crc32 ^= 0xFFFFFFFFu;
    public IncrementalCrc32()
    {
        
    }
    public void AppendData(ReadOnlySequence<byte> bytes)
    {
        foreach (var m in bytes)
        {
            foreach (var b in m.Span)
            {
                _index = (_crc32 ^ b) & 0xff;
                _crc32 = (_crc32 >> 8) ^ Table[_index];
            }
        }
    }
}


