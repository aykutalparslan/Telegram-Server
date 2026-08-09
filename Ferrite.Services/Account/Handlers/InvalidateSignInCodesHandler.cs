// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Handlers;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class InvalidateSignInCodesHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IVerificationCodeService _verificationCodes;

    public InvalidateSignInCodesHandler(IAuthorizationRepository authorizationRepository,
        IVerificationCodeService verificationCodes)
    {
        _authorizationRepository = authorizationRepository;
        _verificationCodes = verificationCodes;
    }

    [TLFunction(Constructors.baseLayer_InvalidateSignInCodes)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var codes = new List<string>();
        var request = new InvalidateSignInCodes(q.AsSpan());
        VectorOfString vector = request.Codes;
        for (int i = 0; i < vector.Count; i++)
        {
            codes.Add(Encoding.UTF8.GetString(vector.ReadTLBytes()));
        }

        if (await VerificationHandlerSupport.GetLoggedInUserIdAsync(_authorizationRepository,
                authKeyId) is null)
        {
            return Error("AUTH_KEY_INVALID"u8);
        }

        await _verificationCodes.InvalidateByCodesAsync(codes);
        return new BoolTrue();
    }

    private static TLBool Error(ReadOnlySpan<byte> message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400, message);
}
