// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Numerics;
using Ferrite.Crypto;
using Ferrite.Core.Execution;
using Ferrite.TL;
using Ferrite.TL.mtproto;

namespace Ferrite.Core.Execution.Functions;

[TLFunction(Constructors.mtproto_ReqPqMulti)]
public class ReqPQFunc : ITLFunction
{
    private IRandomGenerator _randomGenerator;
    private IKeyProvider _keyPairProvider;
    public ReqPQFunc(IRandomGenerator generator, IKeyProvider provider)
    {
        _randomGenerator = generator;
        _keyPairProvider = provider;
    }

    public async ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        byte[] serverNonce;
        if (!ctx.SessionData.ContainsKey("nonce"))
        {
            ctx.SessionData.Add("nonce", new ReqPqMulti(q.AsSpan()).Nonce.ToArray());
            serverNonce = _randomGenerator.GetRandomBytes(16);
            ctx.SessionData.Add("server_nonce", serverNonce);
            await Task.Delay(100);
        }
        else if (!((byte[])ctx.SessionData["nonce"]).AsSpan().SequenceEqual(new ReqPqMulti(q.AsSpan()).Nonce))
        {
            ctx.SessionData["nonce"] = new ReqPqMulti(q.AsSpan()).Nonce.ToArray();
            serverNonce = _randomGenerator.GetRandomBytes(16);
            ctx.SessionData["server_nonce"] = serverNonce;
            return null;
        }

        serverNonce = (byte[])ctx.SessionData["server_nonce"];
        return ProcessInternal(serverNonce, new ReqPqMulti(q.AsSpan()), ctx);
    }

    private TLBytes? ProcessInternal(byte[] serverNonce, ReqPqMulti query, TLExecutionContext ctx)
    {
        byte[] nonce = (byte[])ctx.SessionData["nonce"];
        if (ctx.SessionData.ContainsKey("p"))
        {
            ctx.SessionData.Remove("p");
        }
        if (ctx.SessionData.ContainsKey("q"))
        {
            ctx.SessionData.Remove("q");
        }
        int a = _randomGenerator.GetRandomPrime();
        int b = _randomGenerator.GetRandomPrime();
        BigInteger pq = BigInteger.Multiply(new BigInteger(a), b);
        if (a < b)
        {
            ctx.SessionData.Add("p", a);
            ctx.SessionData.Add("q", b);
        }
        else
        {
            ctx.SessionData.Add("p", b);
            ctx.SessionData.Add("q", a);
        }

        byte[] Pq = pq.ToByteArray(isBigEndian: true);

        var tmp = _keyPairProvider.GetRSAFingerprints();
        var fingerprints = new TL.VectorOfLong();
        foreach (var f in tmp)
        {
            fingerprints.Append(f);
        }
        var resPq = new ResPQ(nonce, 
            serverNonce, Pq, fingerprints);
        return resPq.TLBytes;
    }
}