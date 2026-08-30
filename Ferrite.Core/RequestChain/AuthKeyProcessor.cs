// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Core.Connection;
using Ferrite.Core.Execution;
using Ferrite.Services.Sessions;
using Ferrite.TL;
using Ferrite.TL.mtproto;
using Ferrite.Utils;
using ReqPqMulti = Ferrite.TL.mtproto.ReqPqMulti;

namespace Ferrite.Core.RequestChain;

public class AuthKeyProcessor : ILinkedHandler
{
    private readonly ISessionService _sessionManager;
    private readonly ILogger _log;
    private readonly IExecutionEngine _api;
    public AuthKeyProcessor(ISessionService sessionManager, ILogger log, IExecutionEngine api)
    {
        _sessionManager = sessionManager;
        _log = log;
        _api = api;
    }

    public ILinkedHandler? Next { get; set; }

    public ILinkedHandler SetNext(ILinkedHandler value)
    {
        Next = value;
        return Next;
    }

    public async ValueTask Process(object? sender, TLBytes input, TLExecutionContext ctx)
    {
        var constructor = input.Constructor;
        if (constructor == Constructors.mtproto_ReqPqMulti &&
            sender is MTProtoConnection connection)
        {
            try
            {
                var result = await _api.Invoke(input, ctx);
                if (result == null) return;
                using var response = result.Value;

                Services.Transport.MTProtoMessage message = new Services.Transport.MTProtoMessage();
                message.SessionId = ctx.SessionId;
                message.IsResponse = true;
                message.IsContentRelated = true;
                message.Data = response.AsSpan().ToArray();
                var nonce = new ReqPqMulti(input.AsSpan()).Nonce.ToArray();
                await _sessionManager.AddAuthSessionAsync(nonce,
                    AuthSessionState.FromSessionData(_sessionManager.NodeId,
                        ctx.SessionData),
                    new ActiveSession(connection));
                message.Nonce = nonce;
                message.MessageType = MTProtoMessageType.Unencrypted;
                await connection.SendAsync(message);

                _log.Information("Result for req_pq_multi sent.");
            }
            finally
            {
                input.Dispose();
            }
        }
        else if (constructor == Constructors.mtproto_ReqDhParams)
        {
            try
            {
                var nonce = new TL.mtproto.ReqDhParams(input.AsSpan()).Nonce.ToArray();
                var state = await _sessionManager.GetAuthSessionStateAsync(nonce);
                if (state == null) return;

                RestoreMissingSessionData(ctx, state);
                var result = await _api.Invoke(input, ctx);
                if (result == null) return;
                using var response = result.Value;

                Services.Transport.MTProtoMessage message = new Services.Transport.MTProtoMessage();
                message.SessionId = ctx.SessionId;
                message.IsResponse = true;
                message.IsContentRelated = true;
                message.Data = response.AsSpan().ToArray();
                message.MessageType = MTProtoMessageType.Unencrypted;
                message.Nonce = nonce;

                await _sessionManager.UpdateAuthSessionAsync(nonce,
                    AuthSessionState.FromSessionData(_sessionManager.NodeId,
                        ctx.SessionData));
                if (sender is MTProtoConnection dhConnection)
                {
                    await dhConnection.SendAsync(message);
                }

                _log.Information("Result for req_DH_params sent.");
            }
            finally
            {
                input.Dispose();
            }
        }
        else if (constructor == Constructors.mtproto_SetClientDhParams)
        {
            try
            {
                var nonce = new TL.mtproto.SetClientDhParams(input.AsSpan()).Nonce.ToArray();
                var state = await _sessionManager.GetAuthSessionStateAsync(nonce);
                if (state == null) return;

                RestoreMissingSessionData(ctx, state);
                var result = await _api.Invoke(input, ctx);
                if (result == null) return;
                using var response = result.Value;

                MTProtoMessage message = new Services.Transport.MTProtoMessage();
                message.SessionId = ctx.SessionId;
                message.IsResponse = true;
                message.IsContentRelated = true;
                message.Data = response.AsSpan().ToArray();
                message.MessageType = MTProtoMessageType.Unencrypted;
                message.Nonce = nonce;
                await _sessionManager.UpdateAuthSessionAsync(nonce,
                    AuthSessionState.FromSessionData(_sessionManager.NodeId,
                        ctx.SessionData));
                if (sender is MTProtoConnection clientDhConnection)
                {
                    await clientDhConnection.SendAsync(message);
                }

                _log.Information("Result for set_client_DH_params sent.");
            }
            finally
            {
                input.Dispose();
            }
        }
        else
        {
            if (Next != null) await Next.Process(sender, input, ctx);
            else input.Dispose();
        }
    }

    public async ValueTask Process(object? sender, ITLStreamingObject input, TLExecutionContext ctx)
    {
        if (Next != null) await Next.Process(sender, input, ctx);
    }

    private static void RestoreMissingSessionData(TLExecutionContext context,
        AuthSessionState persisted)
    {
        persisted.RestoreInto(context.SessionData);
    }
}
