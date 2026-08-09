// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer.auth;

namespace Ferrite.Services.Handlers.AuthMethods;

public sealed class ResendCodeHandler
{
    private const int PhoneCodeTimeout = 60;
    private readonly IVerificationCodeService _verificationCodes;

    public ResendCodeHandler(IVerificationCodeService verificationCodes)
    {
        _verificationCodes = verificationCodes;
    }

    [TLFunction(Constructors.baseLayer_ResendCode)]
    public async ValueTask<TLSentCode> Handle(long authKeyId, TLBytes q)
    {
        var request = new ResendCode(q.AsSpan());
        string phoneNumber = Encoding.UTF8.GetString(request.PhoneNumber);
        string phoneCodeHash = Encoding.UTF8.GetString(request.PhoneCodeHash);
        VerificationIssue? issue = await _verificationCodes.ResendAsync(
            VerificationPurpose.LoginPhone, authKeyId, 0, phoneCodeHash);
        if (issue == null)
        {
            return (TLSentCode)RpcErrorGenerator
                .GenerateError(400, "PHONE_CODE_EXPIRED"u8);
        }

        return GenerateSentCode(Encoding.UTF8.GetBytes(issue.Value.PublicHash));
    }

    private static TLSentCode GenerateSentCode(ReadOnlySpan<byte> phoneCodeHash)
    {
        using var codeType = new SentCodeTypeSms(5);
        return SentCode.Builder()
            .Type(codeType.ToReadOnlySpan())
            .Timeout(PhoneCodeTimeout)
            .PhoneCodeHash(phoneCodeHash)
            .Build();
    }
}
