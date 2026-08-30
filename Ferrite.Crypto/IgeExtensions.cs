// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Crypto;

using System;
using System.Security.Cryptography;

public static class IgeExtensions
{
    public static void EncryptIge(this Aes aes, Span<byte> plaintext, Span<byte> iv)
    {
        int len = plaintext.Length / 16;

        Span<byte> y = iv.Slice(0, 16);
        Span<byte> x = iv.Slice(16);
        Span<byte> buf = stackalloc byte[16];

        EncryptIge(aes, plaintext, plaintext, len, y, x, buf);
    }

    public static void EncryptIge(this Aes aes, Span<byte> plaintext, Span<byte> iv,
        Span<byte> ciphertext)
    {
        int len = plaintext.Length / 16;

        Span<byte> y = iv.Slice(0, 16);
        Span<byte> x = iv.Slice(16);
        Span<byte> buf = stackalloc byte[16];

        EncryptIge(aes, plaintext, ciphertext, len, y, x, buf);
    }
    private static void EncryptIge(Aes aes, Span<byte> plaintext,
        Span<byte> ciphertext, int len, Span<byte> y, Span<byte> x, Span<byte> buf)
    {
        Span<byte> block = stackalloc byte[16];
        for (int b = 0; b < len; b++)
        {
            for (int i = 0; i < 16; i++)
            {
                block[i] = plaintext[i + b * 16];
                buf[i] = (byte)(block[i] ^ y[i]);
            }
            buf = aes.EncryptEcb(buf, PaddingMode.None);
            for (int i = 0; i < 16; i++)
            {
                ciphertext[i + b * 16] = y[i] = (byte)(buf[i] ^ x[i]);
            }
            block.CopyTo(x);
        }
    }
    public static void DecryptIge(this Aes aes, Span<byte> ciphertext, Span<byte> iv)
    {
        int len = ciphertext.Length / 16;

        Span<byte> x = iv.Slice(0, 16);
        Span<byte> y = iv.Slice(16);
        Span<byte> buf = stackalloc byte[16];

        DecryptIge(aes, ciphertext, ciphertext, len, x, y, buf);
    }
    public static void DecryptIge(this Aes aes, ReadOnlySpan<byte> ciphertext, Span<byte> iv,
        Span<byte> plaintext)
    {
        int len = ciphertext.Length / 16;

        Span<byte> x = iv.Slice(0, 16);
        Span<byte> y = iv.Slice(16);
        Span<byte> buf = stackalloc byte[16];

        DecryptIge(aes, ciphertext, plaintext, len, x, y, buf);
    }
    private static void DecryptIge(Aes aes, ReadOnlySpan<byte> ciphertext,
        Span<byte> plaintext, int len, Span<byte> x, Span<byte> y, Span<byte> buf)
    {
        Span<byte> block = stackalloc byte[16];
        for (int b = 0; b < len; b++)
        {
            for (int i = 0; i < 16; i++)
            {
                block[i] = ciphertext[i + b * 16];
                buf[i] = (byte)(block[i] ^ y[i]);
            }
            buf = aes.DecryptEcb(buf, PaddingMode.None);
            for (int i = 0; i < 16; i++)
            {
                plaintext[i + b * 16] = y[i] = (byte)(buf[i] ^ x[i]);
            }
            block.CopyTo(x);
        }
    }
}

