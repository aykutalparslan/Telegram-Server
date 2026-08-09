// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class GetTmpPasswordHandler
{
    private readonly IAccountPasswordManager _passwords;

    public GetTmpPasswordHandler(IAccountPasswordManager passwords)
    {
        _passwords = passwords;
    }

    [TLFunction(Constructors.baseLayer_GetTmpPassword)]
    public ValueTask<TLTmpPassword> Handle(long authKeyId, TLBytes q)
    {
        var request = new GetTmpPassword(q.AsSpan());
        return _passwords.GetTemporaryPasswordAsync(authKeyId,
            request.Get_Password(), request.Period);
    }
}
