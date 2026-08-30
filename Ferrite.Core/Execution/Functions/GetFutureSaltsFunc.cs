// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Core.Execution;
using Ferrite.TL;
using Ferrite.TL.mtproto;
using Ferrite.Utils;

namespace Ferrite.Core.Execution.Functions;

[TLFunction(Constructors.mtproto_GetFutureSalts)]
public class GetFutureSaltsFunc : ITLFunction
{
    private readonly IMTProtoService _mtproto;
    private readonly IMTProtoTime _time;

    public GetFutureSaltsFunc(IMTProtoService mtproto, IMTProtoTime time)
    {
        _mtproto = mtproto;
        _time = time;
    }

    public async ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        var request = new GetFutureSalts(q.AsSpan());
        var salts = await _mtproto.GetServerSaltsAsync(ctx.AuthKeyId, request.Num);
        var vector = new VectorBare();
        foreach (var salt in salts)
        {
            using (salt)
            {
                vector.Append(salt.AsSpan());
            }
        }

        using var futureSalts = FutureSalts.Builder()
            .ReqMsgId(ctx.MessageId)
            .Now((int)_time.GetUnixTimeInSeconds())
            .Salts(vector)
            .Build();
        return RpcResultGenerator.Generate(futureSalts.TLBytes!.Value, ctx.MessageId);
    }
}
