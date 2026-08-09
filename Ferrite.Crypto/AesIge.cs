// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Buffers;
using System.Security.Cryptography;

namespace Ferrite.Crypto;

public readonly ref struct AesIge
{
    //TODO: Can we reuse this somehow?
    private readonly Aes _aes;
    private readonly Span<byte> _aesIV;
    public AesIge(Span<byte> authKey, Span<byte> messageKey, bool fromClient = true)
    {
        //x = 0 for messages from client to server and x = 8 for those from server to client.
        int x = 0;
        if (!fromClient)
        {
            x = 8;
        }
        _aes = Aes.Create();
        Span<byte> tmp = stackalloc byte[52];
        //sha256_a = SHA256(msg_key + substr(auth_key, x, 36));
        Span<byte> sha256a = stackalloc byte[32];
        //sha256_b = SHA256 (substr (auth_key, 40+x, 36) + msg_key);
        Span<byte> sha256b = stackalloc byte[32];
        messageKey.CopyTo(tmp);
        authKey.Slice(0+x, 36).CopyTo(tmp.Slice(16));
        SHA256.HashData(tmp, sha256a);
        tmp.Clear();
        authKey.Slice(40+x, 36).CopyTo(tmp);
        messageKey.CopyTo(tmp.Slice(36));
        SHA256.HashData(tmp, sha256b);
        //aes_key = substr(sha256_a, 0, 8) + substr(sha256_b, 8, 16) + substr(sha256_a, 24, 8);
        var _aesKey = new byte[32];
        //aes_iv = substr(sha256_b, 0, 8) + substr(sha256_a, 8, 16) + substr(sha256_b, 24, 8);
        _aesIV = new byte[32];
        sha256a.Slice(0, 8).CopyTo(_aesKey);
        sha256b.Slice(8, 16).CopyTo(_aesKey.AsSpan().Slice(8));
        sha256a.Slice(24, 8).CopyTo(_aesKey.AsSpan().Slice(24));
        sha256b.Slice(0, 8).CopyTo(_aesIV);
        sha256a.Slice(8, 16).CopyTo(_aesIV.Slice(8));
        sha256b.Slice(24, 8).CopyTo(_aesIV.Slice(24));
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

    public static Span<byte> GenerateMessageKey(Span<byte> authKey, Span<byte> plaintext, bool fromClient = false)
    {
        //x = 0 for messages from client to server and x = 8 for those from server to client.
        int x = 0;
        if (!fromClient)
        {
            x = 8;
        }
        var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha256.AppendData(authKey.Slice(88+x, 32));
        sha256.AppendData(plaintext);
        //msg_key_large = SHA256(substr(auth_key, 88 + x, 32) + plaintext + random_padding);
        Span<byte> messageKeyLarge = sha256.GetCurrentHash();
        //msg_key = substr (msg_key_large, 8, 16);
        Span<byte> messageKey = messageKeyLarge.Slice(8,16);
        return messageKey;
    }
    public static Span<byte> GenerateMessageKey(Span<byte> authKey, ReadOnlySequence<byte> plaintext, bool fromClient = false)
    {
        //x = 0 for messages from client to server and x = 8 for those from server to client.
        int x = 0;
        if (!fromClient)
        {
            x = 8;
        }
        var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha256.AppendData(authKey.Slice(88 + x, 32));
        foreach (var memory in plaintext)
        {
            sha256.AppendData(memory.Span);
        }
        //msg_key_large = SHA256(substr(auth_key, 88 + x, 32) + plaintext + random_padding);
        Span<byte> messageKeyLarge = sha256.GetCurrentHash();
        //msg_key = substr (msg_key_large, 8, 16);
        Span<byte> messageKey = messageKeyLarge.Slice(8, 16);
        return messageKey;
    }
    public static Span<byte> GenerateMessageKey(Span<byte> authKey, Stream plaintext, bool fromClient = false)
    {
        //x = 0 for messages from client to server and x = 8 for those from server to client.
        int x = 0;
        if (!fromClient)
        {
            x = 8;
        }
        var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha256.AppendData(authKey.Slice(88 + x, 32));
        int remaining = (int)plaintext.Length;
        var buffer = new byte[1024];
        while (remaining > 0)
        {
            var read = plaintext.Read(buffer, 0, buffer.Length);
            sha256.AppendData(buffer.AsSpan(0, read));
            remaining -= read;
        }
        //msg_key_large = SHA256(substr(auth_key, 88 + x, 32) + plaintext + random_padding);
        Span<byte> messageKeyLarge = sha256.GetCurrentHash();
        //msg_key = substr (msg_key_large, 8, 16);
        Span<byte> messageKey = messageKeyLarge.Slice(8, 16);
        return messageKey;
    }
    public static void GenerateAesKeyAndIV(Span<byte> authKey, Span<byte> messageKey, 
        bool fromClient, Span<byte> aesKey, Span<byte> aesIV)
    {
        //x = 0 for messages from client to server and x = 8 for those from server to client.
        int x = 0;
        if (!fromClient)
        {
            x = 8;
        }
        Span<byte> tmp = stackalloc byte[52];
        //sha256_a = SHA256(msg_key + substr(auth_key, x, 36));
        Span<byte> sha256a = stackalloc byte[32];
        //sha256_b = SHA256 (substr (auth_key, 40+x, 36) + msg_key);
        Span<byte> sha256b = stackalloc byte[32];
        messageKey.CopyTo(tmp);
        authKey.Slice(0+x, 36).CopyTo(tmp.Slice(16));
        SHA256.HashData(tmp, sha256a);
        tmp.Clear();
        authKey.Slice(40+x, 36).CopyTo(tmp);
        messageKey.CopyTo(tmp.Slice(36));
        SHA256.HashData(tmp, sha256b);
        //aes_key = substr(sha256_a, 0, 8) + substr(sha256_b, 8, 16) + substr(sha256_a, 24, 8);
        sha256a.Slice(0, 8).CopyTo(aesKey);
        sha256b.Slice(8, 16).CopyTo(aesKey.Slice(8));
        sha256a.Slice(24, 8).CopyTo(aesKey.Slice(24));
        //aes_iv = substr(sha256_b, 0, 8) + substr(sha256_a, 8, 16) + substr(sha256_b, 24, 8);
        sha256b.Slice(0, 8).CopyTo(aesIV);
        sha256a.Slice(8, 16).CopyTo(aesIV.Slice(8));
        sha256b.Slice(24, 8).CopyTo(aesIV.Slice(24));
    }
}

