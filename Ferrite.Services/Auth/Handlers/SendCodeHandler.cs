// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer.auth;

namespace Ferrite.Services.Handlers.AuthMethods;

public sealed class SendCodeHandler
{
    private const int PhoneCodeTimeout = 60;
    private readonly IVerificationCodeService _verificationCodes;

    public SendCodeHandler(IVerificationCodeService verificationCodes)
    {
        _verificationCodes = verificationCodes;
    }

    [TLFunction(Constructors.baseLayer_SendCode)]
    public async ValueTask<TLSentCode> Handle(long authKeyId, TLBytes q)
    {
        var request = new SendCode(q.AsSpan());
        string phoneNumber = Encoding.UTF8.GetString(request.PhoneNumber);
        VerificationIssue issue = await _verificationCodes.IssueSmsAsync(
            VerificationPurpose.LoginPhone, authKeyId, 0, phoneNumber);

        using var codeType = new SentCodeTypeSms(5);
        return SentCode.Builder()
            .Type(codeType.ToReadOnlySpan())
            .Timeout(PhoneCodeTimeout)
            .PhoneCodeHash(Encoding.UTF8.GetBytes(issue.PublicHash))
            .Build();
    }
}
