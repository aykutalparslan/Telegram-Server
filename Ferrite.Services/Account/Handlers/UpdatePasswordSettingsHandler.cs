// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class UpdatePasswordSettingsHandler
{
    private readonly IAccountPasswordManager _passwords;

    public UpdatePasswordSettingsHandler(IAccountPasswordManager passwords)
    {
        _passwords = passwords;
    }

    [TLFunction(Constructors.baseLayer_UpdatePasswordSettings)]
    public ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = new UpdatePasswordSettings(q.AsSpan());
        return _passwords.UpdatePasswordSettingsAsync(authKeyId,
            request.Get_Password(), request.Get_NewSettings());
    }
}
