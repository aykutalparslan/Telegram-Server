// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class GetPasswordSettingsHandler
{
    private readonly IAccountPasswordManager _passwords;

    public GetPasswordSettingsHandler(IAccountPasswordManager passwords)
    {
        _passwords = passwords;
    }

    [TLFunction(Constructors.baseLayer_GetPasswordSettings)]
    public ValueTask<TLPasswordSettings> Handle(long authKeyId, TLBytes q)
    {
        var request = new GetPasswordSettings(q.AsSpan());
        return _passwords.GetPasswordSettingsAsync(authKeyId,
            request.Get_Password());
    }
}
