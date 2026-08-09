// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Handlers;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;
using Ferrite.TL.baseLayer.auth;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class SendVerifyPhoneCodeHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IVerificationCodeService _verificationCodes;

    public SendVerifyPhoneCodeHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        IVerificationCodeService verificationCodes)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _verificationCodes = verificationCodes;
    }

    [TLFunction(Constructors.baseLayer_SendVerifyPhoneCode)]
    public async ValueTask<TLSentCode> Handle(long authKeyId, TLBytes q)
    {
        var request = new SendVerifyPhoneCode(q);
        string phoneNumber = Encoding.UTF8.GetString(request.PhoneNumber);

        long? userId = await VerificationHandlerSupport
            .GetLoggedInUserIdAsync(_authorizationRepository, authKeyId);
        if (userId is null)
        {
            return Error("AUTH_KEY_INVALID"u8);
        }

        VerificationIssue issue = await _verificationCodes.IssueSmsAsync(
            VerificationPurpose.VerifyPhone, authKeyId, userId.Value,
            phoneNumber);
        return VerificationHandlerSupport.BuildSentCode(issue);
    }

    private static TLSentCode Error(ReadOnlySpan<byte> message) =>
        (TLSentCode)RpcErrorGenerator.GenerateError(400, message);
}
