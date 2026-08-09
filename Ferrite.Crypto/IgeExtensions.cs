// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

/*
 *  IGE (Infinite Garble Extension) chaining sequences are as follows:
 *  
 *      Encryption: y_i = f(x_i ⊕ y_{i−1}) ⊕ x_{i−1}
 *      Decryption: x_i = f^-1(y_i ⊕ x_{i-1}) ⊕ y_{i-1}
 *  
 *  Gligor, V. D., Donescu, P., & Katz, J. (2000, November). 
 *  On message integrity in symmetric encryption. 
 *  In 1st NIST Workshop on AES Modes of Operation.
 */


namespace Ferrite.Crypto;

using System;
using System.Security.Cryptography;

/// <summary>
/// Provides extensions to perform AES(256) IGE Encryption used by Telegram
/// </summary>
public static class IgeExtensions
{
    /// <summary>
    /// Encrypts data using IGE mode with no padding. The encryption occurs
    /// in place and replaces the plaintext with the ciphertext.
    /// </summary>
    /// <param name="plaintext">The data to encrypt.</param>
    /// <param name="iv">The initialization vector.</param>
    public static void EncryptIge(this Aes aes, Span<byte> plaintext, Span<byte> iv)
    {
        int len = plaintext.Length / 16;

        Span<byte> y = iv.Slice(0, 16);
        Span<byte> x = iv.Slice(16);
        Span<byte> buf = stackalloc byte[16];

        EncryptIge(aes, plaintext, plaintext, len, y, x, buf);
    }

    /// <summary>
    /// Encrypts data using IGE mode with no padding.
    /// </summary>
    /// <param name="plaintext">The data to encrypt.</param>
    /// <param name="iv">The initialization vector.</param>
    public static void EncryptIge(this Aes aes, Span<byte> plaintext, Span<byte> iv,
        Span<byte> ciphertext)
    {
        int len = plaintext.Length / 16;

        Span<byte> y = iv.Slice(0, 16);
        Span<byte> x = iv.Slice(16);
        Span<byte> buf = stackalloc byte[16];

        EncryptIge(aes, plaintext, ciphertext, len, y, x, buf);
    }
    /// <summary>
    /// Performs the IGE chain sequence for encryption.
    /// </summary>
    /// <param name="aes"></param>
    /// <param name="plaintext">The data to encrypt.</param>
    /// <param name="ciphertext"The data after encryption.></param>
    /// <param name="len">Number of blocks to encrypt.</param>
    /// <param name="y">The first block of the initialization vector.</param>
    /// <param name="x">The second block of the initialization vector.</param>
    /// <param name="buf">The block buffer.</param>
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
    /// <summary>
    /// Decrypts data using IGE mode with no padding. The decryption occurs
    /// in place and replaces the ciphertext with the plaintext.
    /// </summary>
    /// <param name="ciphertext">The data to decrypt. Replaced with the
    /// decrypted version.</param>
    /// <param name="iv">The initialization vector.</param>
    public static void DecryptIge(this Aes aes, Span<byte> ciphertext, Span<byte> iv)
    {
        int len = ciphertext.Length / 16;

        Span<byte> x = iv.Slice(0, 16);
        Span<byte> y = iv.Slice(16);
        Span<byte> buf = stackalloc byte[16];

        DecryptIge(aes, ciphertext, ciphertext, len, x, y, buf);
    }
    /// <summary>
    /// Decrypts data using IGE mode with no padding.
    /// </summary>
    /// <param name="ciphertext">The data to decrypt.</param>
    /// <param name="iv">The initialization vector.</param>
    /// <param name="plaintext">The data after decryption.</param>
    public static void DecryptIge(this Aes aes, ReadOnlySpan<byte> ciphertext, Span<byte> iv,
        Span<byte> plaintext)
    {
        int len = ciphertext.Length / 16;

        Span<byte> x = iv.Slice(0, 16);
        Span<byte> y = iv.Slice(16);
        Span<byte> buf = stackalloc byte[16];

        DecryptIge(aes, ciphertext, plaintext, len, x, y, buf);
    }
    /// <summary>
    /// Performs the IGE chain sequence for decryption.
    /// </summary>
    /// <param name="aes"></param>
    /// <param name="ciphertext"The data to decrypt></param>
    /// <param name="plaintext">The data after decryption.</param>
    /// <param name="len">Number of blocks to decrypt.</param>
    /// <param name="y">The first block of the initialization vector.</param>
    /// <param name="x">The second block of the initialization vector.</param>
    /// <param name="buf">The block buffer.</param>
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


