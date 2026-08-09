// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Services;
using Ferrite.Core.Execution;
using Ferrite.TL;
using Ferrite.TL.mtproto;

namespace Ferrite.Core.Execution.Functions;

[TLFunction(Constructors.mtproto_DestroySession)]
public class DestroySessionFunc : ITLFunction
{
    private readonly ISessionService _sessions;

    public DestroySessionFunc(ISessionService sessions)
    {
        _sessions = sessions;
    }

    public async ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        var request = new DestroySession(q.AsSpan());
        long sessionId = request.SessionId;

        // destroy_session is an MTProto service message: the client asks the
        // server to forget a DIFFERENT session belonging to the same auth key.
        // The response is a top-level destroy_session_ok / destroy_session_none
        // message (NOT wrapped in rpc_result -- Android tgnet, iOS MtProtoKit,
        // and TDLib all parse DestroySessionRes as a top-level mtproto object).
        var sessions = await _sessions.GetSessionsAsync(ctx.CurrentAuthKeyId);
        bool exists = sessions.Any(s => s.SessionId == sessionId);
        if (exists)
        {
            await _sessions.RemoveSession(ctx.CurrentAuthKeyId, sessionId);
            return (TLBytes)DestroySessionOk.Builder().SessionId(sessionId).Build().TLBytes!;
        }

        return (TLBytes)DestroySessionNone.Builder().SessionId(sessionId).Build().TLBytes!;
    }
}
