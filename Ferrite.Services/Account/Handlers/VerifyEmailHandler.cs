// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Net.Mail;
using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Gateway;
using Ferrite.Services.Handlers;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class VerifyEmailHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private enum VerificationKind
    {
        Code,
        Google,
        Apple,
    }

    private readonly IUnitOfWork _unitOfWork;
    private readonly IVerificationCodeService _verificationCodes;
    private readonly IEmailIdentityTokenValidator _identityTokens;

    public VerifyEmailHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        IVerificationCodeService verificationCodes,
        IEmailIdentityTokenValidator identityTokens)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _verificationCodes = verificationCodes;
        _identityTokens = identityTokens;
    }

    [TLFunction(Constructors.baseLayer_VerifyEmail)]
    public async ValueTask<TLEmailVerified> Handle(long authKeyId, TLBytes q)
    {
        var request = new VerifyEmail(q);
        if (!EmailVerificationHandlerSupport.TryParsePurpose(
                request.Get_PurposeView(), out EmailVerificationHandlerSupport.PurposeRequest purpose) ||
            !TryParseVerification(request.Get_VerificationView(),
                out VerificationKind verificationKind, out string credential))
        {
            return Error("EMAIL_VERIFY_INVALID"u8);
        }

        EmailVerificationHandlerSupport.PurposeBinding? binding =
            await EmailVerificationHandlerSupport.BindAsync(purpose, _authorizationRepository, _verificationCodes, authKeyId);
        if (binding is null)
        {
            return Error(purpose.IsLoginSetup
                ? "PHONE_CODE_EXPIRED"u8
                : "AUTH_KEY_INVALID"u8);
        }

        string? email;
        if (verificationKind == VerificationKind.Code)
        {
            VerifiedChallenge? verified = await _verificationCodes
                .VerifyActiveAsync(binding.Value.Purpose, authKeyId,
                    binding.Value.SubjectId, credential);
            email = verified?.Destination;
        }
        else
        {
            EmailIdentityTokenProvider provider = verificationKind ==
                VerificationKind.Google
                ? EmailIdentityTokenProvider.Google
                : EmailIdentityTokenProvider.Apple;
            EmailIdentityTokenValidationResult validated =
                await _identityTokens.ValidateAsync(
                    new EmailIdentityTokenValidationRequest(provider,
                        credential));
            email = validated.IsValid ? validated.Email : null;
        }

        if (string.IsNullOrWhiteSpace(email) ||
            !MailAddress.TryCreate(email, out MailAddress? parsed) ||
            !StringComparer.OrdinalIgnoreCase.Equals(parsed.Address, email))
        {
            return Error("EMAIL_VERIFY_INVALID"u8);
        }

        if (!binding.Value.IsLoginSetup)
        {
            return EmailVerified.Builder()
                .Email(Encoding.UTF8.GetBytes(email))
                .Build();
        }

        VerificationIssue issue = await _verificationCodes.IssueSmsAsync(
            VerificationPurpose.LoginPhone, authKeyId, 0,
            binding.Value.PhoneNumber);
        using var sentCode = VerificationHandlerSupport.BuildSentCode(issue);
        return EmailVerifiedLogin.Builder()
            .Email(Encoding.UTF8.GetBytes(email))
            .SentCode(sentCode.AsSpan())
            .Build();
    }

    private static bool TryParseVerification(EmailVerificationView verification,
        out VerificationKind kind, out string credential)
    {
        if (verification.Is(out EmailVerificationCode code))
        {
            kind = VerificationKind.Code;
            credential = Encoding.UTF8.GetString(code.Code);
            return !string.IsNullOrWhiteSpace(credential);
        }
        if (verification.Is(out EmailVerificationGoogle google))
        {
            kind = VerificationKind.Google;
            credential = Encoding.UTF8.GetString(google.Token);
            return !string.IsNullOrWhiteSpace(credential);
        }
        if (verification.Is(out EmailVerificationApple apple))
        {
            kind = VerificationKind.Apple;
            credential = Encoding.UTF8.GetString(apple.Token);
            return !string.IsNullOrWhiteSpace(credential);
        }

        kind = default;
        credential = string.Empty;
        return false;
    }

    private static TLEmailVerified Error(ReadOnlySpan<byte> message) =>
        (TLEmailVerified)RpcErrorGenerator.GenerateError(400, message);
}
