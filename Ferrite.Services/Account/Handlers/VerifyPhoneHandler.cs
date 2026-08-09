// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Handlers;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class VerifyPhoneHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IVerificationCodeService _verificationCodes;

    public VerifyPhoneHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        IVerificationCodeService verificationCodes)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _verificationCodes = verificationCodes;
    }

    [TLFunction(Constructors.baseLayer_VerifyPhone)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = new VerifyPhone(q.AsSpan());
        string phoneNumber = Encoding.UTF8.GetString(request.PhoneNumber);
        string phoneCodeHash = Encoding.UTF8.GetString(request.PhoneCodeHash);
        string phoneCode = Encoding.UTF8.GetString(request.PhoneCode);

        long? userId = await VerificationHandlerSupport
            .GetLoggedInUserIdAsync(_authorizationRepository, authKeyId);
        if (userId is null)
        {
            return Error("AUTH_KEY_INVALID"u8);
        }

        VerifiedChallenge? active = await _verificationCodes.GetActiveAsync(
            VerificationPurpose.VerifyPhone, authKeyId, userId.Value,
            phoneCodeHash);
        if (active is not { } challenge ||
            !StringComparer.Ordinal.Equals(challenge.Destination, phoneNumber))
        {
            return Error("PHONE_CODE_EXPIRED"u8);
        }

        return await _verificationCodes.VerifyAsync(
            VerificationPurpose.VerifyPhone, authKeyId, userId.Value,
            phoneCodeHash, phoneCode) is not null
            ? new BoolTrue()
            : Error("PHONE_CODE_INVALID"u8);
    }

    private static TLBool Error(ReadOnlySpan<byte> message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400, message);
}
