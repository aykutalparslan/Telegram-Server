// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using DotNext.Buffers;
using Ferrite.Crypto;
using Ferrite.Core.Execution;
using Ferrite.TL;
using Ferrite.TL.mtproto;

namespace Ferrite.Core.Execution.Functions;

[TLFunction(Constructors.mtproto_SetClientDhParams)]
public class SetClientDhParamsFunc : ITLFunction
{
    private readonly IMTProtoService _mtproto;
    public SetClientDhParamsFunc(IMTProtoService mtproto)
    {
        _mtproto = mtproto;
    }
    public ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        return new ValueTask<TLBytes?>(ProcessInternal(new TL.mtproto.SetClientDhParams(q.AsSpan()), ctx));
    }

    private TLBytes? ProcessInternal(TL.mtproto.SetClientDhParams query, TLExecutionContext ctx)
    {
        bool failed = false;
        var sessionNonce = (byte[])ctx.SessionData["nonce"];
        var sessionServerNonce = (byte[])ctx.SessionData["server_nonce"];
        if (!query.Nonce.SequenceEqual(sessionNonce) ||
            !query.ServerNonce.SequenceEqual(sessionServerNonce))
        {
            failed = true;
        }

        Aes aes = Aes.Create();
        aes.Key = (byte[])ctx.SessionData["temp_aes_key"];
        using var encryptedData = UnmanagedMemory.Allocate<byte>(query.EncryptedData.Length);
        aes.DecryptIge(query.EncryptedData, ((byte[])ctx.SessionData["temp_aes_iv"]).ToArray(),
            encryptedData.Span);
        var sha1Received = encryptedData.Span[..20].ToArray();
        var dataWithPadding = encryptedData.Memory[20..];
        var len = ClientDhInnerData.ReadSize(dataWithPadding.Span, 0);
        var clientDhInnerData = new ClientDhInnerData(dataWithPadding.Span[..len]);
        var sha1Actual = SHA1.HashData(clientDhInnerData.ToReadOnlySpan());
        if (!sha1Actual.SequenceEqual(sha1Received) ||
            !query.Nonce.SequenceEqual(sessionNonce) ||
            !query.ServerNonce.SequenceEqual(sessionServerNonce) ||
            !clientDhInnerData.Nonce.SequenceEqual(sessionNonce) ||
            !clientDhInnerData.ServerNonce.SequenceEqual(sessionServerNonce))
        {
            failed = true;
        }

        BigInteger prime = new BigInteger(TelegramDhParameters.Prime, true, true);
        BigInteger g_b = new BigInteger(clientDhInnerData.GB, true, true);
        BigInteger g = new BigInteger((int)ctx.SessionData["g"]);
        BigInteger a = new BigInteger((byte[])ctx.SessionData["a"], true, true);
        var authKey = AuthKeyBlock(BigInteger.ModPow(g_b, a, prime));
        ctx.SessionData.Add("auth_key", authKey);
        var authKeySHA1 = SHA1.HashData(authKey);
        var authKeyHash = MemoryMarshal.Cast<byte, long>(authKeySHA1.AsSpan().Slice(12))[0];
        var authKeyAuxHash = authKeySHA1.Take(8).ToArray();
        var newNonceHash1 = SHA1.HashData(((byte[])ctx.SessionData["new_nonce"]).Concat(new byte[1] { 1 })
            .Concat(authKeyAuxHash).ToArray()).Skip(4).ToArray();
        var newNonceHash3 = SHA1.HashData(((byte[])ctx.SessionData["new_nonce"])
                .Concat(new byte[1] { 2 }).Concat(authKeyAuxHash).ToArray())
            .Skip(4).ToArray();
        BigInteger min = BigInteger.Pow(new BigInteger(2), 2048 - 64);
        BigInteger max = BigInteger.Subtract(prime, min);
        if (g_b.CompareTo(min) <= 0 || g_b.CompareTo(max) >= 0 || failed)
        {
            var dhGenFail = new DhGenFail(sessionNonce, sessionServerNonce, newNonceHash3);
            return dhGenFail.TLBytes;
        }

        bool temp_auth_key = false;
        if(ctx.SessionData.TryGetValue("temp_auth_key", out var key))
        {
            temp_auth_key = (bool)key;
        }
        
        var existingKey = temp_auth_key
            ? _mtproto.GetTempAuthKey(authKeyHash)
            : _mtproto.GetAuthKey(authKeyHash);
        if (existingKey == null || existingKey.Length == 0)
        {
            var authKeyTrimmed = authKey.AsSpan().Slice(0, 192).ToArray();
            if (temp_auth_key)
            {
                int expiresIn = (int)ctx.SessionData["temp_auth_key_expires_in"]; 
                _mtproto.PutTempAuthKey(authKeyHash, authKeyTrimmed, new TimeSpan(0, 0, expiresIn));
            }
            else
            {
                _mtproto.PutAuthKey(authKeyHash, authKeyTrimmed);
            }

            Span<byte> serverSaltBytes = stackalloc byte[8];
            var newNonce = (byte[])ctx.SessionData["new_nonce"];
            for (int i = 0; i < serverSaltBytes.Length; i++)
            {
                serverSaltBytes[i] = (byte)(newNonce[i] ^ sessionServerNonce[i]);
            }
            _mtproto.PutServerSalt(authKeyHash,
                BinaryPrimitives.ReadInt64LittleEndian(serverSaltBytes), 1800);

            var dhGenOk = new DhGenOk(sessionNonce, sessionServerNonce, newNonceHash1);
            ctx.SessionData.Clear();
            return dhGenOk.TLBytes;
        }
        else
        {
            var newNonceHash2 = SHA1.HashData(((byte[])ctx.SessionData["new_nonce"])
                    .Concat(new byte[1] { 2 }).Concat(authKeyAuxHash).ToArray())
                .Skip(4).ToArray();
            var dhGenRetry = new DhGenRetry(sessionNonce, sessionServerNonce, newNonceHash2);
            return dhGenRetry.TLBytes;
        }
    }

    private static byte[] AuthKeyBlock(BigInteger sharedSecret)
    {
        byte[] encoded = sharedSecret.ToByteArray(true, true);
        if (encoded.Length == 256)
        {
            return encoded;
        }

        var block = new byte[256];
        encoded.CopyTo(block, 256 - encoded.Length);
        return block;
    }
}
