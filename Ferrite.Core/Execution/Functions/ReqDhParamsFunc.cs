// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using System.Numerics;
using System.Security.Cryptography;
using DotNext.Buffers;
using Ferrite.Crypto;
using Ferrite.Core.Execution;
using Ferrite.TL;
using Ferrite.TL.mtproto;
using Ferrite.Utils;

namespace Ferrite.Core.Execution.Functions;

[TLFunction(Constructors.mtproto_ReqDhParams)]
public class ReqDhParamsFunc : ITLFunction
{
    private readonly IKeyProvider _keyProvider;
    private readonly ILogger _log;
    private readonly IRandomGenerator _random;
    private readonly int[] _gs = new int[] { 3, 4, 7 };
    public ReqDhParamsFunc(IKeyProvider provider, IRandomGenerator generator, ILogger logger)
    {
        _keyProvider = provider;
        _random = generator;
        this._log = logger;
    }
    public ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        return new ValueTask<TLBytes?>(ProcessInternal(new TL.mtproto.ReqDhParams(q.AsSpan()), ctx));
    }

    private TLBytes? ProcessInternal(TL.mtproto.ReqDhParams query, TLExecutionContext ctx)
    {
        var rsaKey = _keyProvider.GetKey(query.PublicKeyFingerprint);
        if (rsaKey == null)
        {
            var rpcError = new RpcError(-404, ""u8);
            _log.Debug("Could not obtain the RSA Key.");
            return rpcError.TLBytes;
        }
        if(!ctx.SessionData.ContainsKey("nonce") || 
                !ctx.SessionData.ContainsKey("server_nonce"))
        {
            var rpcError = new RpcError(-404, ""u8);
            _log.Debug("Session is empty.");
            return rpcError.TLBytes;
        }
        Memory<byte> data;
        byte[] sha256;
        RSAPad(query.EncryptedData.ToArray() ,rsaKey, out data, out sha256);

        if (!sha256.AsSpan().SequenceEqual(data.Span.Slice(224)))
        {
            _log.Debug("SHA256 did not match.");
            var rpcError = new TL.mtproto.RpcError(-404, ""u8);
            return rpcError.TLBytes;
        }

        var innerData = new TLBytes(data.Slice(32), 0, data.Length - 32);
        var constructor = innerData.Constructor;
        
        var sessionNonce = (byte[])ctx.SessionData["nonce"];
        var sessionServerNonce = (byte[])ctx.SessionData["server_nonce"];
        if (constructor == Constructors.mtproto_PQInnerData)
        {
            var len = PQInnerData.ReadSize(data.Span, 32);
            var pQInnerData = new PQInnerData(data.Span.Slice(32, len));
            ctx.SessionData.Add("new_nonce", pQInnerData.NewNonce.ToArray());
            if (!query.Nonce.SequenceEqual(pQInnerData.Nonce) ||
                !query.Nonce.SequenceEqual(sessionNonce) ||
                !query.ServerNonce.SequenceEqual(pQInnerData.ServerNonce) ||
                !query.ServerNonce.SequenceEqual(sessionServerNonce))
            {
                var rpcError = new RpcError(-404, "Nonce values did not match."u8);
                return rpcError.TLBytes;
            }
            var inner_new_nonce = pQInnerData.NewNonce.ToArray();
            var newNonceServerNonce = SHA1.HashData((inner_new_nonce)
                .Concat((byte[])sessionServerNonce).ToArray());
            var serverNonceNewNonce = SHA1.HashData(((byte[])sessionServerNonce)
                .Concat(inner_new_nonce).ToArray());
            var newNonceNewNonce = SHA1.HashData((inner_new_nonce)
                .Concat(inner_new_nonce).ToArray());
            var tmpAesKey = newNonceServerNonce
                .Concat(serverNonceNewNonce.SkipLast(8)).ToArray();
            var tmpAesIV = serverNonceNewNonce.Skip(12)
                .Concat(newNonceNewNonce).Concat((inner_new_nonce).SkipLast(28)).ToArray();
            ctx.SessionData.Add("temp_aes_key", tmpAesKey.ToArray());
            ctx.SessionData.Add("temp_aes_iv", tmpAesIV.ToArray());
            using var answer = GenerateEncryptedAnswer(ctx, sessionNonce, sessionServerNonce, tmpAesKey, tmpAesIV);
            var serverDhParamsOk = new ServerDhParamsOk(query.Nonce, query.ServerNonce,answer.Memory.Span);
            
            return serverDhParamsOk.TLBytes;
        }
        else if (constructor == Constructors.mtproto_PQInnerDataDc)
        {
            var len = PQInnerDataDc.ReadSize(data.Span, 32);
            var pQInnerDataDc = new PQInnerDataDc(data.Span.Slice(32, len));
            ctx.SessionData.Add("new_nonce", pQInnerDataDc.NewNonce.ToArray());
            if (!query.Nonce.SequenceEqual(pQInnerDataDc.Nonce) ||
                !query.Nonce.SequenceEqual(sessionNonce) ||
                !query.ServerNonce.SequenceEqual(pQInnerDataDc.ServerNonce) ||
                !query.ServerNonce.SequenceEqual(sessionServerNonce))
            {
                var rpcError = new RpcError(-404, "Nonce values did not match."u8);
                return rpcError.TLBytes;
            }
            var inner_new_nonce = pQInnerDataDc.NewNonce.ToArray();
            var newNonceServerNonce = SHA1.HashData((inner_new_nonce)
                .Concat((byte[])sessionServerNonce).ToArray());
            var serverNonceNewNonce = SHA1.HashData(((byte[])sessionServerNonce)
                .Concat(inner_new_nonce).ToArray());
            var newNonceNewNonce = SHA1.HashData((inner_new_nonce)
                .Concat(inner_new_nonce).ToArray());
            var tmpAesKey = newNonceServerNonce
                .Concat(serverNonceNewNonce.SkipLast(8)).ToArray();
            var tmpAesIV = serverNonceNewNonce.Skip(12)
                .Concat(newNonceNewNonce).Concat((inner_new_nonce).SkipLast(28)).ToArray();
            ctx.SessionData.Add("temp_aes_key", tmpAesKey.ToArray());
            ctx.SessionData.Add("temp_aes_iv", tmpAesIV.ToArray());
            using var answer = GenerateEncryptedAnswer(ctx, sessionNonce, sessionServerNonce, tmpAesKey, tmpAesIV);
            var serverDhParamsOk = new ServerDhParamsOk(query.Nonce, query.ServerNonce,answer.Memory.Span);
            return serverDhParamsOk.TLBytes;
        }
        else if (constructor == Constructors.mtproto_PQInnerDataTempDc)
        {
            var len = PQInnerDataTempDc.ReadSize(data.Span, 32);
            var pQInnerDataTempDc = new PQInnerDataTempDc(data.Span.Slice(32, len));
            ctx.SessionData.Add("temp_auth_key", true);
            ctx.SessionData.Add("temp_auth_key_expires_in", pQInnerDataTempDc.ExpiresIn);
            ctx.SessionData.Add("new_nonce", pQInnerDataTempDc.NewNonce.ToArray());
            if (!query.Nonce.SequenceEqual(pQInnerDataTempDc.Nonce) ||
                !query.Nonce.SequenceEqual(sessionNonce) ||
                !query.ServerNonce.SequenceEqual(pQInnerDataTempDc.ServerNonce) ||
                !query.ServerNonce.SequenceEqual(sessionServerNonce))
            {
                var rpcError = new RpcError(-404, "Nonce values did not match."u8);
                return rpcError.TLBytes;
            }
            var inner_new_nonce = pQInnerDataTempDc.NewNonce.ToArray();
            var newNonceServerNonce = SHA1.HashData((inner_new_nonce)
                .Concat((byte[])sessionServerNonce).ToArray());
            var serverNonceNewNonce = SHA1.HashData(((byte[])sessionServerNonce)
                .Concat(inner_new_nonce).ToArray());
            var newNonceNewNonce = SHA1.HashData((inner_new_nonce)
                .Concat(inner_new_nonce).ToArray());
            var tmpAesKey = newNonceServerNonce
                .Concat(serverNonceNewNonce.SkipLast(8)).ToArray();
            var tmpAesIV = serverNonceNewNonce.Skip(12)
                .Concat(newNonceNewNonce).Concat((inner_new_nonce).SkipLast(28)).ToArray();
            ctx.SessionData.Add("temp_aes_key", tmpAesKey.ToArray());
            ctx.SessionData.Add("temp_aes_iv", tmpAesIV.ToArray());
            using var answer = GenerateEncryptedAnswer(ctx, sessionNonce, sessionServerNonce, tmpAesKey, tmpAesIV);
            ctx.SessionData.Add("valid_until", DateTime.Now.AddSeconds(pQInnerDataTempDc.ExpiresIn));
            var serverDhParamsOk = new ServerDhParamsOk(query.Nonce, query.ServerNonce,answer.Memory.Span);
            return serverDhParamsOk.TLBytes;
        }
        else if (constructor == Constructors.mtproto_PQInnerDataTemp)
        {
            var len = PQInnerDataTemp.ReadSize(data.Span, 32);
            var pQInnerDataTemp = new PQInnerDataTemp(data.Span.Slice(32, len));
            ctx.SessionData.Add("temp_auth_key", true);
            ctx.SessionData.Add("temp_auth_key_expires_in", pQInnerDataTemp.ExpiresIn);
            ctx.SessionData.Add("new_nonce", pQInnerDataTemp.NewNonce.ToArray());
            if (!query.Nonce.SequenceEqual(pQInnerDataTemp.Nonce) ||
                !query.Nonce.SequenceEqual(sessionNonce) ||
                !query.ServerNonce.SequenceEqual(pQInnerDataTemp.ServerNonce) ||
                !query.ServerNonce.SequenceEqual(sessionServerNonce))
            {
                var rpcError = new RpcError(-404, "Nonce values did not match."u8);
                return rpcError.TLBytes;
            }
            var inner_new_nonce = pQInnerDataTemp.NewNonce.ToArray();
            var newNonceServerNonce = SHA1.HashData((inner_new_nonce)
                .Concat((byte[])sessionServerNonce).ToArray());
            var serverNonceNewNonce = SHA1.HashData(((byte[])sessionServerNonce)
                .Concat(inner_new_nonce).ToArray());
            var newNonceNewNonce = SHA1.HashData((inner_new_nonce)
                .Concat(inner_new_nonce).ToArray());
            var tmpAesKey = newNonceServerNonce
                .Concat(serverNonceNewNonce.SkipLast(8)).ToArray();
            var tmpAesIV = serverNonceNewNonce.Skip(12)
                .Concat(newNonceNewNonce).Concat((inner_new_nonce).SkipLast(28)).ToArray();
            ctx.SessionData.Add("temp_aes_key", tmpAesKey.ToArray());
            ctx.SessionData.Add("temp_aes_iv", tmpAesIV.ToArray());
            using var answer = GenerateEncryptedAnswer(ctx, sessionNonce, sessionServerNonce, tmpAesKey, tmpAesIV);
            ctx.SessionData.Add("valid_until", DateTime.Now.AddSeconds(pQInnerDataTemp.ExpiresIn));
            var serverDhParamsOk = new ServerDhParamsOk(query.Nonce, query.ServerNonce,answer.Memory.Span);
            return serverDhParamsOk.TLBytes;
        }
        return null;
    }
    private IMemoryOwner<byte> GenerateEncryptedAnswer(TLExecutionContext ctx, byte[] sessionNonce, byte[] sessionServerNonce, byte[] tmpAesKey, byte[] tmpAesIV)
    {
        BigInteger prime = new BigInteger(TelegramDhParameters.Prime, true, true);
        BigInteger min = BigInteger.Pow(new BigInteger(2), 2048 - 64);
        BigInteger max = BigInteger.Subtract(prime, min);
        BigInteger a = _random.GetRandomInteger(2, BigInteger.Subtract(prime, 2));
        BigInteger g = new BigInteger(_gs[_random.GetRandomNumber(_gs.Length)]);
        BigInteger g_a = BigInteger.ModPow(g, a, prime);
        while (g_a.CompareTo(min) <= 0 || g_a.CompareTo(max) >= 0)
        {
            a = _random.GetRandomInteger(2, BigInteger.Subtract(prime, 2));
            g_a = BigInteger.ModPow(g, a, prime);
        }
        
        var innerNonce = sessionNonce;
        var innerServerNonce = sessionServerNonce;
        var innerDhPrime = TelegramDhParameters.Prime;
        var innerG = (int)g;
        var innerGA = g_a.ToByteArray(true, true);
        var innerServerTime = (int)DateTimeOffset.Now.ToUnixTimeSeconds();

        using var serverDhInnerData = new ServerDhInnerData(innerNonce, innerServerNonce, innerG,
            innerDhPrime, innerGA, innerServerTime);
        
        ctx.SessionData.Add("g", innerG);
        ctx.SessionData.Add("a", a.ToByteArray(true,true));
        ctx.SessionData.Add("g_a", innerGA);
        int len = 20 + serverDhInnerData.Length;
        while (len % 16 != 0)
        {
            len++;
        }

        var answerWithHash = UnmanagedMemory.Allocate<byte>(len);
        var innerSpan = serverDhInnerData.ToReadOnlySpan();
        SHA1.HashData( innerSpan, answerWithHash.Span[..20]);
        innerSpan.CopyTo(answerWithHash.Span[20..]);

        Aes aes = Aes.Create();
        aes.Key = tmpAesKey;
        aes.EncryptIge(answerWithHash.Span, tmpAesIV);
        return answerWithHash;
    }

    private void RSAPad(byte[] encryptedData, IRSAKey rsaKey, out Memory<byte> data, out byte[] sha256)
    {
        data = rsaKey.DecryptBlock(encryptedData).AsMemory();
        Span<byte> tempKey = data.Slice(0, 32).Span;
        Span<byte> aesEncrypted = data.Slice(32).Span;

        byte[] sha256AesEncrypted = SHA256.HashData(aesEncrypted);
        for (int i = 0; i < 32; i++)
        {
            tempKey[i] = (byte)(tempKey[i] ^ sha256AesEncrypted[i]);
        }
        Aes aes = Aes.Create();
        aes.Key = tempKey.ToArray();
        aes.DecryptIge(aesEncrypted, stackalloc byte[32]);
        Span<byte> dataPadReversed = aesEncrypted.Slice(0, 192);
        dataPadReversed.Reverse();
        sha256 = SHA256.HashData(data.Slice(0, 224).Span);
    }
}
