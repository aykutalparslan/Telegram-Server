// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using TLAccountPassword = Ferrite.TL.baseLayer.account.TLPassword;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class GetPasswordHandler
{
    private readonly IAccountPasswordManager _passwords;

    public GetPasswordHandler(IAccountPasswordManager passwords)
    {
        _passwords = passwords;
    }

    [TLFunction(Constructors.baseLayer_GetPassword)]
    public ValueTask<TLAccountPassword> Handle(long authKeyId, TLBytes q) =>
        _passwords.GetPasswordAsync(authKeyId);
}
