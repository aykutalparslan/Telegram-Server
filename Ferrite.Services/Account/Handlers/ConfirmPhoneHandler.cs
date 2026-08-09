// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Handlers;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class ConfirmPhoneHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IVerificationCodeService _verificationCodes;

    public ConfirmPhoneHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        IVerificationCodeService verificationCodes)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _verificationCodes = verificationCodes;
    }

    [TLFunction(Constructors.baseLayer_ConfirmPhone)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = new ConfirmPhone(q.AsSpan());
        string phoneCodeHash = Encoding.UTF8.GetString(request.PhoneCodeHash);
        string phoneCode = Encoding.UTF8.GetString(request.PhoneCode);

        long? userId = await VerificationHandlerSupport
            .GetLoggedInUserIdAsync(_authorizationRepository, authKeyId);
        if (userId is null)
        {
            return Error("AUTH_KEY_INVALID"u8);
        }

        VerifiedChallenge? active = await _verificationCodes.GetActiveAsync(
            VerificationPurpose.ConfirmPhone, authKeyId, userId.Value,
            phoneCodeHash);
        if (active is not { } challenge || challenge.Context.Length == 0)
        {
            return Error("PHONE_CODE_EXPIRED"u8);
        }

        return await _verificationCodes.VerifyAsync(
            VerificationPurpose.ConfirmPhone, authKeyId, userId.Value,
            phoneCodeHash, phoneCode) is not null
            ? new BoolTrue()
            : Error("PHONE_CODE_INVALID"u8);
    }

    private static TLBool Error(ReadOnlySpan<byte> message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400, message);
}
