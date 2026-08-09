// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Net.Mail;
using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Handlers;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class SendVerifyEmailCodeHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IVerificationCodeService _verificationCodes;

    public SendVerifyEmailCodeHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        IVerificationCodeService verificationCodes)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _verificationCodes = verificationCodes;
    }

    [TLFunction(Constructors.baseLayer_SendVerifyEmailCode)]
    public async ValueTask<TLSentEmailCode> Handle(long authKeyId, TLBytes q)
    {
        var request = new SendVerifyEmailCode(q);
        string email = Encoding.UTF8.GetString(request.Email);
        if (!EmailVerificationHandlerSupport.TryParsePurpose(
                request.Get_PurposeView(), out EmailVerificationHandlerSupport.PurposeRequest purpose))
        {
            return Error("EMAIL_VERIFY_PURPOSE_INVALID"u8);
        }

        if (!MailAddress.TryCreate(email, out MailAddress? parsed) ||
            !StringComparer.OrdinalIgnoreCase.Equals(parsed.Address, email))
        {
            return Error("EMAIL_INVALID"u8);
        }

        EmailVerificationHandlerSupport.PurposeBinding? binding =
            await EmailVerificationHandlerSupport.BindAsync(purpose, _authorizationRepository, _verificationCodes, authKeyId);
        if (binding is null)
        {
            return Error(purpose.IsLoginSetup
                ? "PHONE_CODE_EXPIRED"u8
                : "AUTH_KEY_INVALID"u8);
        }

        VerificationIssue issue = await _verificationCodes.IssueEmailAsync(
            binding.Value.Purpose, authKeyId, binding.Value.SubjectId, email);
        return SentEmailCode.Builder()
            .EmailPattern(Encoding.UTF8.GetBytes(
                VerificationHandlerSupport.MaskEmail(email)))
            .LengthProperty(issue.CodeLength)
            .Build();
    }

    private static TLSentEmailCode Error(ReadOnlySpan<byte> message) =>
        (TLSentEmailCode)RpcErrorGenerator.GenerateError(400, message);
}
