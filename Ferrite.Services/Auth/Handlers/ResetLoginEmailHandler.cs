// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.auth;

namespace Ferrite.Services.Handlers.AuthMethods;

public sealed class ResetLoginEmailHandler
{
    private readonly IVerificationCodeService _verificationCodes;

    public ResetLoginEmailHandler(IVerificationCodeService verificationCodes)
    {
        _verificationCodes = verificationCodes;
    }

    [TLFunction(Constructors.baseLayer_ResetLoginEmail)]
    public async ValueTask<TLSentCode> Handle(long authKeyId, TLBytes q)
    {
        var request = new ResetLoginEmail(q.AsSpan());
        string phoneNumber = Encoding.UTF8.GetString(request.PhoneNumber);
        string phoneCodeHash = Encoding.UTF8.GetString(request.PhoneCodeHash);

        VerifiedChallenge? active = await _verificationCodes.GetActiveAsync(
            VerificationPurpose.LoginPhone, authKeyId, 0, phoneCodeHash);
        if (active is not { } challenge ||
            !StringComparer.Ordinal.Equals(challenge.Destination, phoneNumber))
        {
            return Error("PHONE_CODE_EXPIRED"u8);
        }

        VerificationIssue issue = await _verificationCodes.IssueSmsAsync(
            VerificationPurpose.LoginPhone, authKeyId, 0, phoneNumber);
        return VerificationHandlerSupport.BuildSentCode(issue);
    }

    private static TLSentCode Error(ReadOnlySpan<byte> message) =>
        (TLSentCode)RpcErrorGenerator.GenerateError(400, message);
}
