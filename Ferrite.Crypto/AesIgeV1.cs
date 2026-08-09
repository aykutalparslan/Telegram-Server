// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Buffers;
using System.Security.Cryptography;

namespace Ferrite.Crypto;

public readonly ref struct AesIgeV1
{
    //TODO: Can we reuse this somehow?
    private readonly Aes _aes;
    private readonly Span<byte> _aesIV;
    public AesIgeV1(Span<byte> authKey, Span<byte> messageKey, bool fromClient = true)
    {
        //x = 0 for messages from client to server and x = 8 for those from server to client.
        int x = 0;
        if (!fromClient)
        {
            x = 8;
        }
        _aes = Aes.Create();
        Span<byte> tmp = stackalloc byte[48];
        //sha1_a = SHA1 (msg_key + substr (auth_key, x, 32));
        Span<byte> sha1a = stackalloc byte[20];
        //sha1_b = SHA1 (substr (auth_key, 32+x, 16) + msg_key + substr (auth_key, 48+x, 16));
        Span<byte> sha1b = stackalloc byte[20];
        //sha1_с = SHA1(substr(auth_key, 64 + x, 32) + msg_key);
        Span<byte> sha1c = stackalloc byte[20];
        //sha1_d = SHA1 (msg_key + substr (auth_key, 96+x, 32));
        Span<byte> sha1d = stackalloc byte[20];
        messageKey.CopyTo(tmp);
        authKey.Slice(0+x, 32).CopyTo(tmp.Slice(16));
        SHA1.HashData(tmp, sha1a);
        tmp.Clear();
        authKey.Slice(32+x, 16).CopyTo(tmp);
        messageKey.CopyTo(tmp.Slice(16));
        authKey.Slice(48 + x, 16).CopyTo(tmp.Slice(32));
        SHA1.HashData(tmp, sha1b);
        tmp.Clear();
        authKey.Slice(64 + x, 32).CopyTo(tmp);
        messageKey.CopyTo(tmp.Slice(32));
        SHA1.HashData(tmp, sha1c);
        tmp.Clear();
        messageKey.CopyTo(tmp);
        authKey.Slice(96 + x, 32).CopyTo(tmp.Slice(16));
        SHA1.HashData(tmp, sha1d);
        //aes_key = substr(sha1_a, 0, 8) + substr(sha1_b, 8, 12) + substr(sha1_c, 4, 12);
        var _aesKey = new byte[32];
        //aes_iv = substr (sha1_a, 8, 12) + substr (sha1_b, 0, 8) + substr (sha1_c, 16, 4) + substr (sha1_d, 0, 8);
        _aesIV = new byte[32];
        sha1a.Slice(0, 8).CopyTo(_aesKey);
        sha1b.Slice(8, 12).CopyTo(_aesKey.AsSpan().Slice(8));
        sha1c.Slice(4, 12).CopyTo(_aesKey.AsSpan().Slice(20));
        sha1a.Slice(8, 12).CopyTo(_aesIV);
        sha1b.Slice(0, 8).CopyTo(_aesIV.Slice(12));
        sha1c.Slice(16, 4).CopyTo(_aesIV.Slice(20));
        sha1d.Slice(0, 8).CopyTo(_aesIV.Slice(24));
        _aes.Key = _aesKey;
    }

    public void Encrypt(Span<byte> message)
    {
        _aes.EncryptIge(message, _aesIV);
    }

    public void Encrypt(Span<byte> source, Span<byte> destination)
    {
        _aes.EncryptIge(source, _aesIV, destination);
    }

    public void Decrypt(Span<byte> message)
    {
        _aes.DecryptIge(message, _aesIV);
    }

    public void Decrypt(Span<byte> source, Span<byte> destination)
    {
        _aes.DecryptIge(source, _aesIV, destination);
    }

    public static Span<byte> GenerateMessageKey(Span<byte> authKey, Span<byte> plaintext)
    {
        var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        sha1.AppendData(plaintext);
        //msg_key = substr (SHA1 (plaintext), 4, 16);
        Span<byte> messageKeyLarge = sha1.GetCurrentHash();
        Span<byte> messageKey = messageKeyLarge.Slice(4, 16);
        return messageKey;
    }
    public static Span<byte> GenerateMessageKey(Span<byte> authKey, ReadOnlySequence<byte> plaintext)
    {
        var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        foreach (var memory in plaintext)
        {
            sha1.AppendData(memory.Span);
        }
        //msg_key = substr (SHA1 (plaintext), 4, 16);
        Span<byte> messageKeyLarge = sha1.GetCurrentHash();
        Span<byte> messageKey = messageKeyLarge.Slice(4, 16);
        return messageKey;
    }
}

