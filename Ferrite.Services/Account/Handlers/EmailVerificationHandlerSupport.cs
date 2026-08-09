// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Handlers;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Handlers.AccountMethods;

internal static class EmailVerificationHandlerSupport
{
    internal readonly record struct PurposeRequest(VerificationPurpose Purpose,
        bool IsLoginSetup, string PhoneNumber, string PhoneCodeHash);

    internal readonly record struct PurposeBinding(VerificationPurpose Purpose,
        long SubjectId, bool IsLoginSetup, string PhoneNumber);

    public static bool TryParsePurpose(EmailVerifyPurposeView purpose,
        out PurposeRequest request)
    {
        if (purpose.Is(out EmailVerifyPurposeLoginSetup loginSetup))
        {
            request = new PurposeRequest(VerificationPurpose.LoginEmailSetup,
                true, Encoding.UTF8.GetString(loginSetup.PhoneNumber),
                Encoding.UTF8.GetString(loginSetup.PhoneCodeHash));
            return true;
        }
        if (purpose.Is(out EmailVerifyPurposeLoginChange _))
        {
            request = new PurposeRequest(VerificationPurpose.LoginEmailChange,
                false, string.Empty, string.Empty);
            return true;
        }
        if (purpose.Is(out EmailVerifyPurposePassport _))
        {
            request = new PurposeRequest(VerificationPurpose.VerifyEmail,
                false, string.Empty, string.Empty);
            return true;
        }

        request = default;
        return false;
    }

    public static async ValueTask<PurposeBinding?> BindAsync(
        PurposeRequest request, IAuthorizationRepository authorizationRepository,
        IVerificationCodeService verificationCodes, long authKeyId)
    {
        if (request.IsLoginSetup)
        {
            VerifiedChallenge? active = await verificationCodes.GetActiveAsync(
                VerificationPurpose.LoginPhone, authKeyId, 0,
                request.PhoneCodeHash);
            if (active is not { } challenge ||
                !StringComparer.Ordinal.Equals(challenge.Destination,
                    request.PhoneNumber))
            {
                return null;
            }
            return new PurposeBinding(request.Purpose, 0, true,
                request.PhoneNumber);
        }

        long? userId = await VerificationHandlerSupport
            .GetLoggedInUserIdAsync(authorizationRepository, authKeyId);
        return userId is { } id
            ? new PurposeBinding(request.Purpose, id, false, string.Empty)
            : null;
    }
}
