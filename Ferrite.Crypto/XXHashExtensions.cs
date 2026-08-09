// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

// https://github.com/aykutalparslan/xxhash/blob/main/XXHashExtensions.cs

// Implements xxHash fast digest algorithm
//      * https://github.com/Cyan4973/xxHash/blob/dev/doc/xxhash_spec.md
// original-header 
// Copyright (c) Yann Collet
// Permission is granted to copy and distribute this document for any
// purpose and without charge, including translations into other languages and
// incorporation into compilations, provided that the copyright notice and this
// notice are preserved, and that any substantive changes or deletions from the
// original are clearly marked. Distribution of this document is unlimited.
// original-header 

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Numerics.BitOperations;

namespace xxHash;
public static class XXHashExtensions
{
    const uint Prime32First = 0x9E3779B1u;
    const uint Prime32Second = 0x85EBCA77u;
    const uint Prime32Third = 0xC2B2AE3Du;
    const uint Prime32Fourth = 0x27D4EB2Fu;
    const uint Prime32Fifth = 0x165667B1u;
    const ulong Prime64First = 0x9E3779B185EBCA87u;
    const ulong Prime64Second = 0xC2B2AE3D27D4EB4Fu;
    const ulong Prime64Third = 0x165667B19E3779F9u;
    const ulong Prime64Fourth = 0x85EBCA77C2B2AE63u;
    const ulong Prime64Fifth = 0x27D4EB2F165667C5u;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Round(ref uint acc1, in uint lane1,
        ref uint acc2, in uint lane2, ref uint acc3, in uint lane3,
        ref uint acc4, in uint lane4)
    {
        acc1 = acc1 + (lane1 * Prime32Second);
        acc1 = RotateLeft(acc1, 13);
        acc1 = acc1 * Prime32First;

        acc2 = acc2 + (lane2 * Prime32Second);
        acc2 = RotateLeft(acc2, 13);
        acc2 = acc2 * Prime32First;

        acc3 = acc3 + (lane3 * Prime32Second);
        acc3 = RotateLeft(acc3, 13);
        acc3 = acc3 * Prime32First;

        acc4 = acc4 + (lane4 * Prime32Second);
        acc4 = RotateLeft(acc4, 13);
        acc4 = acc4 * Prime32First;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Round(ref ulong acc1, in ulong lane1,
        ref ulong acc2, in ulong lane2, ref ulong acc3, in ulong lane3,
        ref ulong acc4, in ulong lane4)
    {
        acc1 = acc1 + (lane1 * Prime64Second);
        acc1 = RotateLeft(acc1, 31);
        acc1 = acc1 * Prime64First;

        acc2 = acc2 + (lane2 * Prime64Second);
        acc2 = RotateLeft(acc2, 31);
        acc2 = acc2 * Prime64First;

        acc3 = acc3 + (lane3 * Prime64Second);
        acc3 = RotateLeft(acc3, 31);
        acc3 = acc3 * Prime64First;

        acc4 = acc4 + (lane4 * Prime64Second);
        acc4 = RotateLeft(acc4, 31);
        acc4 = acc4 * Prime64First;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Round(ulong acc, in ulong lane)
    {
        acc = acc + (lane * Prime64Second);
        acc = RotateLeft(acc, 31);
        acc = acc * Prime64First;
        return acc;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong mergeAccumulator(ulong acc, ulong accN)
    {
        acc = acc ^ Round(0, accN);
        acc = acc * Prime64First;
        return acc + Prime64Fourth;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessRemaining(ReadOnlySpan<byte> data,
        ReadOnlySpan<uint> blocks, int numblocks, ref uint acc,
        ref uint lane, ref int remaining, ref int position)
    {
        while (remaining >= 4)
        {
            lane = blocks[numblocks * 4 + position / 4];
            acc = acc + lane * Prime32Third;
            acc = RotateLeft(acc, 17) * Prime32Fourth;
            position += 4;
            remaining -= 4;
        }
        lane = 0;
        while (remaining >= 1)
        {
            lane = data[numblocks * 16 + position];
            acc = acc + lane * Prime32Fifth;
            acc = RotateLeft(acc, 11) * Prime32First;
            position += 1;
            remaining -= 1;
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessRemaining(ReadOnlySpan<byte> data,
        ReadOnlySpan<ulong> blocks, int numblocks, ref ulong acc,
        ref ulong lane, ref int remaining, ref int position)
    {
        while (remaining >= 8)
        {
            lane = blocks[numblocks * 4 + position / 8];
            acc = acc ^ Round(0, lane);
            acc = RotateLeft(acc, 27) * Prime64First;
            acc = acc + Prime64Fourth;
            position += 8;
            remaining -= 8;
        }
        if (remaining >= 4)
        {
            lane = MemoryMarshal.Cast<byte, uint>(data)[numblocks * 8 + position / 4];
            acc = acc ^ (lane * Prime64First);
            acc = RotateLeft(acc, 23) * Prime64Second;
            acc = acc + Prime64Third;
            position += 4;
            remaining -= 4;
        }

        while (remaining >= 1)
        {
            lane = data[numblocks * 32 + position];
            acc = acc ^ (lane * Prime64Fifth);
            acc = RotateLeft(acc, 11) * Prime64First;
            position += 1;
            remaining -= 1;
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Avalanche(ref uint acc)
    {
        acc = acc ^ (acc >> 15);
        acc = acc * Prime32Second;
        acc = acc ^ (acc >> 13);
        acc = acc * Prime32Third;
        acc = acc ^ (acc >> 16);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Avalanche(ref ulong acc)
    {
        acc = acc ^ (acc >> 33);
        acc = acc * Prime64Second;
        acc = acc ^ (acc >> 29);
        acc = acc * Prime64Third;
        acc = acc ^ (acc >> 32);
    }
    public static uint GetXxHash32(this byte[] data, uint seed = 0)
    {
        return XxHash32(data, seed);
    }
    public static uint GetXxHash32(this Span<byte> data, uint seed = 0)
    {
        return XxHash32(data, seed);
    }
    private static uint XxHash32(ReadOnlySpan<byte> data, uint seed)
    {
        int numblocks = data.Length / 16;
        int remaining = 0;
        int position = 0;
        uint acc1 = seed + Prime32First + Prime32Second;
        uint acc2 = seed + Prime32Second;
        uint acc3 = seed + 0;
        uint acc4 = seed - Prime32First;
        uint acc = 0;
        uint lane = 0;
        ReadOnlySpan<uint> blocks = MemoryMarshal.Cast<byte, uint>(data);
        if (data.Length < 16)
        {
            acc = seed + Prime32Fifth;
            acc = acc + (uint)data.Length;
            remaining = data.Length - numblocks * 16;
            position = 0;
            ProcessRemaining(data, blocks, numblocks, ref acc,
                ref lane, ref remaining, ref position);
            Avalanche(ref acc);
            return acc;
        }
        for (int i = 0; i < numblocks; i++)
        {
            Round(ref acc1, blocks[i * 4],
                        ref acc2, blocks[i * 4 + 1],
                        ref acc3, blocks[i * 4 + 2],
                        ref acc4, blocks[i * 4 + 3]);
        }
        acc = RotateLeft(acc1, 1) + RotateLeft(acc2, 7) + RotateLeft(acc3, 12)
                + RotateLeft(acc4, 18);

        acc = acc + (uint)data.Length;

        remaining = data.Length - numblocks * 16;
        position = 0;

        ProcessRemaining(data, blocks, numblocks, ref acc, ref lane,
            ref remaining, ref position);
        acc = acc ^ (acc >> 15);
        acc = acc * Prime32Second;
        acc = acc ^ (acc >> 13);
        acc = acc * Prime32Third;
        acc = acc ^ (acc >> 16);
        return acc;
    }
    public static ulong GetXxHash64(this byte[] data, uint seed = 0)
    {
        return XxHash64(data, seed);
    }
    public static ulong GetXxHash64(this Span<byte> data, uint seed = 0)
    {
        return XxHash64(data, seed);
    }
    public static ulong GetXxHash64(this ReadOnlySpan<byte> data, uint seed = 0)
    {
        return XxHash64(data, seed);
    }
    private static ulong XxHash64(ReadOnlySpan<byte> data, uint seed)
    {
        int numblocks = data.Length / 32;
        int remaining = 0;
        int position = 0;
        ulong acc1 = seed + Prime64First + Prime64Second;
        ulong acc2 = seed + Prime64Second;
        ulong acc3 = seed + 0;
        ulong acc4 = seed - Prime64First;
        ulong acc = 0;
        ulong lane = 0;
        ReadOnlySpan<ulong> blocks = MemoryMarshal.Cast<byte, ulong>(data);
        if (data.Length < 32)
        {
            acc = seed + Prime64Fifth;
            acc = acc + (ulong)data.Length;
            remaining = data.Length;
            position = 0;
            ProcessRemaining(data, blocks, numblocks, ref acc,
                ref lane, ref remaining, ref position);
            Avalanche(ref acc);
            return acc;
        }
        for (int i = 0; i < numblocks; i++)
        {
            Round(ref acc1, blocks[i * 4],
                        ref acc2, blocks[i * 4 + 1],
                        ref acc3, blocks[i * 4 + 2],
                        ref acc4, blocks[i * 4 + 3]);
        }
        acc = RotateLeft(acc1, 1) + RotateLeft(acc2, 7) + RotateLeft(acc3, 12)
            + RotateLeft(acc4, 18);
        acc = mergeAccumulator(acc, acc1);
        acc = mergeAccumulator(acc, acc2);
        acc = mergeAccumulator(acc, acc3);
        acc = mergeAccumulator(acc, acc4);
        acc = acc + (ulong)data.Length;

        remaining = data.Length - numblocks * 32;
        position = 0;
        ProcessRemaining(data, blocks, numblocks, ref acc, ref lane,
            ref remaining, ref position);
        Avalanche(ref acc);
        return acc;
    }
}