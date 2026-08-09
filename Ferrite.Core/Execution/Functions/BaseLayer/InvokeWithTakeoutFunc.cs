// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Services;
using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Core.Execution.Functions.BaseLayer;

[TLFunction(Constructors.baseLayer_InvokeWithTakeout)]
public sealed class InvokeWithTakeoutFunc : ITLFunction
{
    private readonly IAccountSettingsRepository _settings;
    private readonly TimeProvider _time;

    public InvokeWithTakeoutFunc(IAccountSettingsRepository settings,
        TimeProvider time)
    {
        _settings = settings;
        _time = time;
    }

    public IExecutionEngine? ExecutionEngine { get; set; }

    public async ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        long id = new TL.baseLayer.InvokeWithTakeout(q.AsSpan()).TakeoutId;
        using TLTakeoutSessionState? session = await _settings
            .GetTakeoutSessionAsync(id);
        if (session is null ||
            session.Value.AsTakeoutSessionState().AuthKeyId !=
            ctx.CurrentAuthKeyId || session.Value.AsTakeoutSessionState()
                .ExpiresAt <= _time.GetUtcNow().ToUnixTimeSeconds())
        {
            return RpcErrorGenerator.GenerateError(400, "TAKEOUT_INVALID"u8);
        }

        using TLBytes query = RequestUnwrapper.InvokeWithTakeoutQuery(q, out _);
        return ExecutionEngine is null
            ? null : await ExecutionEngine.Invoke(query, ctx);
    }
}
