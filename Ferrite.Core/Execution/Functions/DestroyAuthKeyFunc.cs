// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Services;
using Ferrite.Core.Execution;
using Ferrite.TL;
using Ferrite.TL.mtproto;

namespace Ferrite.Core.Execution.Functions;

[TLFunction(Constructors.mtproto_DestroyAuthKey)]
public class DestroyAuthKeyFunc : ITLFunction
{
    private readonly IMTProtoService _mtproto;

    public DestroyAuthKeyFunc(IMTProtoService mtproto)
    {
        _mtproto = mtproto;
    }

    public async ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        // destroy_auth_key destroys the PERMANENT auth key of the current
        // connection (CurrentAuthKeyId resolves a temp/PFS key to its permanent
        // key). The response is a top-level destroy_auth_key_ok / _none / _fail
        // message (NOT wrapped in rpc_result -- Android tgnet, iOS MtProtoKit,
        // and TDLib all parse DestroyAuthKeyRes as a top-level mtproto object,
        // mirroring destroy_session).
        long authKeyId = ctx.CurrentAuthKeyId;

        var existing = await _mtproto.GetAuthKeyAsync(authKeyId);
        if (existing == null)
        {
            return (TLBytes)DestroyAuthKeyNone.Builder().Build().TLBytes!;
        }

        bool destroyed = await _mtproto.DestroyAuthKeyAsync(authKeyId);
        return destroyed
            ? (TLBytes)DestroyAuthKeyOk.Builder().Build().TLBytes!
            : (TLBytes)DestroyAuthKeyFail.Builder().Build().TLBytes!;
    }
}
