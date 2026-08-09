// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Services.Gateway;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.auth;

namespace Ferrite.Services.Handlers.AuthMethods;

public sealed class RequestFirebaseSmsHandler
{
    private readonly IVerificationCodeService _verificationCodes;
    private readonly IDeviceAttestationTokenValidator _validator;

    public RequestFirebaseSmsHandler(IVerificationCodeService verificationCodes,
        IDeviceAttestationTokenValidator validator)
    {
        _verificationCodes = verificationCodes;
        _validator = validator;
    }

    [TLFunction(Constructors.baseLayer_RequestFirebaseSms)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        DeviceAttestationTokenValidationRequest? validation = null;
        var request = new RequestFirebaseSms(q.AsSpan());
        string phoneNumber = Encoding.UTF8.GetString(request.PhoneNumber);
        string phoneCodeHash = Encoding.UTF8.GetString(request.PhoneCodeHash);

        int count = 0;
        if (request.Flags[0])
        {
            count++;
            validation = CreateValidation(DeviceAttestationTokenKind.SafetyNet,
                request.SafetyNetToken, phoneNumber, phoneCodeHash);
        }
        if (request.Flags[2])
        {
            count++;
            validation = CreateValidation(
                DeviceAttestationTokenKind.GooglePlayIntegrity,
                request.PlayIntegrityToken, phoneNumber, phoneCodeHash);
        }
        if (request.Flags[1])
        {
            count++;
            validation = CreateValidation(
                DeviceAttestationTokenKind.ApplePushSecret,
                request.IosPushSecret, phoneNumber, phoneCodeHash);
        }
        bool ambiguous = count != 1;

        VerifiedChallenge? active = await _verificationCodes.GetActiveAsync(
            VerificationPurpose.LoginPhone, authKeyId, 0, phoneCodeHash);
        if (active is not { } challenge ||
            !StringComparer.Ordinal.Equals(challenge.Destination, phoneNumber))
        {
            return Error("PHONE_CODE_EXPIRED"u8);
        }
        if (ambiguous || validation is null ||
            string.IsNullOrWhiteSpace(validation.Token) ||
            !await _validator.ValidateAsync(validation))
        {
            return Error("FIREBASE_VERIFY_FAILED"u8);
        }

        return await _verificationCodes.ReissueSmsInPlaceAsync(
            VerificationPurpose.LoginPhone, authKeyId, 0, phoneCodeHash)
            ? new BoolTrue()
            : Error("PHONE_CODE_EXPIRED"u8);
    }

    private static DeviceAttestationTokenValidationRequest CreateValidation(
        DeviceAttestationTokenKind kind, ReadOnlySpan<byte> token,
        string phoneNumber, string phoneCodeHash) =>
        new(kind, Encoding.UTF8.GetString(token), phoneNumber: phoneNumber,
            phoneCodeHash: phoneCodeHash);

    private static TLBool Error(ReadOnlySpan<byte> message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400, message);
}
