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

public sealed class SendConfirmPhoneCodeHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IVerificationCodeService _verificationCodes;

    public SendConfirmPhoneCodeHandler(IAuthorizationRepository authorizationRepository,
        IVerificationCodeService verificationCodes)
    {
        _authorizationRepository = authorizationRepository;
        _verificationCodes = verificationCodes;
    }

    [TLFunction(Constructors.baseLayer_SendConfirmPhoneCode)]
    public async ValueTask<TLSentCode> Handle(long authKeyId, TLBytes q)
    {
        var request = new SendConfirmPhoneCode(q);
        string deletionHash = Encoding.UTF8.GetString(request.Hash);

        VerificationHandlerSupport.AuthenticatedUser? user =
            await VerificationHandlerSupport.GetLoggedInUserAsync(_authorizationRepository,
                authKeyId);
        if (user is null)
        {
            return Error("AUTH_KEY_INVALID"u8);
        }

        VerifiedChallenge? seeded = await _verificationCodes.GetActiveAsync(
            VerificationPurpose.ConfirmPhone, authKeyId, user.Value.UserId,
            deletionHash);
        if (seeded is not { } challenge || challenge.Context.Length == 0 ||
            !StringComparer.Ordinal.Equals(challenge.Destination,
                user.Value.Phone))
        {
            return Error("HASH_INVALID"u8);
        }

        VerificationIssue? issue = await _verificationCodes.ResendAsync(
            VerificationPurpose.ConfirmPhone, authKeyId, user.Value.UserId,
            deletionHash);
        return issue is { } sent
            ? VerificationHandlerSupport.BuildSentCode(sent)
            : Error("HASH_INVALID"u8);
    }

    private static TLSentCode Error(ReadOnlySpan<byte> message) =>
        (TLSentCode)RpcErrorGenerator.GenerateError(400, message);
}
