// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Crypto;
using Ferrite.Core.Execution;
using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Core.Execution.Functions.BaseLayer;

[TLFunction(Constructors.baseLayer_InitConnection)]
public class InitConnectionFunc : ITLFunction
{
    public IExecutionEngine? ExecutionEngine { get; set; }
    private readonly IRandomGenerator _random;
    private readonly IAuthService _auth;

    public InitConnectionFunc(IRandomGenerator random, IAuthService auth)
    {
        _random = random;
        _auth = auth;
    }

    public async ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        using var info = CreateAppInfo(q, ctx, _random);
        await _auth.SaveAppInfo(info);
        using var query = RequestUnwrapper.InitConnectionQuery(q);
        if (ExecutionEngine != null) return await ExecutionEngine.Invoke(query, ctx);
        return null;
    }

    internal static TLAppInfo CreateAppInfo(TLBytes q, TLExecutionContext ctx,
        IRandomGenerator random)
    {
        var request = (TL.baseLayer.InitConnection)q;
        return AppInfo.Builder()
            .Hash(random.NextLong())
            .ApiId(request.ApiId)
            .AppVersion(request.AppVersion)
            .AuthKeyId(ctx.CurrentAuthKeyId)
            .DeviceModel(request.DeviceModel)
            .Ip(Encoding.UTF8.GetBytes(ctx.IP))
            .LangCode(request.LangCode)
            .LangPack(request.LangPack)
            .SystemLangCode(request.LangCode)
            .SystemVersion(request.SystemVersion)
            .Build();
    }
}
