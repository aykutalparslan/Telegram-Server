// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Core.Execution;
using Ferrite.TL;
using Layer104 = Ferrite.TL.layer104.auth;
using Current = Ferrite.TL.baseLayer.auth;

namespace Ferrite.Core.Execution.Functions.BaseLayer.Auth;

[TLFunction(Constructors.layer104_AuthSignUp)]
public class SignUpLayer104Func : ITLFunction
{
    private readonly IAuthService _auth;

    public SignUpLayer104Func(IAuthService auth)
    {
        _auth = auth;
    }

    public async ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
    {
        Console.WriteLine($"*** Layer104SignUp authKey={ctx.AuthKeyId} msgId={ctx.MessageId} " +
                          $"session={ctx.SessionId} permKey={ctx.PermAuthKeyId} ***");
        using var current = ToCurrent(q);
        using var authorization = await _auth.SignUp(ctx.CurrentAuthKeyId, current);
        return RpcResultGenerator.Generate(authorization, ctx.MessageId);
    }

    private static TLBytes ToCurrent(TLBytes q)
    {
        var sent = new Layer104.AuthSignUp(q.AsSpan());
        var current = Current.SignUp.Builder()
            .PhoneNumber(sent.PhoneNumber)
            .PhoneCodeHash(sent.PhoneCodeHash)
            .FirstName(sent.FirstName)
            .LastName(sent.LastName)
            .Build();
        return current.TLBytes!.Value;
    }
}
