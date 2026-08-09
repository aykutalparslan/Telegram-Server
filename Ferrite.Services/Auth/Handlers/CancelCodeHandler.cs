// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer.auth;

namespace Ferrite.Services.Handlers.AuthMethods;

public sealed class CancelCodeHandler
{
    private readonly IVerificationCodeService _verificationCodes;

    public CancelCodeHandler(IVerificationCodeService verificationCodes)
    {
        _verificationCodes = verificationCodes;
    }

    [TLFunction(Constructors.baseLayer_CancelCode)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = new CancelCode(q.AsSpan());
        string phoneNumber = Encoding.UTF8.GetString(request.PhoneNumber);
        string phoneCodeHash = Encoding.UTF8.GetString(request.PhoneCodeHash);
        bool result = await _verificationCodes.CancelAsync(
            VerificationPurpose.LoginPhone, authKeyId, 0, phoneCodeHash);
        return result ? new BoolTrue() : new BoolFalse();
    }
}
