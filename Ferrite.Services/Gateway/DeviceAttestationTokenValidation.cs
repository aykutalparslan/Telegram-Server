// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Gateway;

public enum DeviceAttestationTokenKind
{
    SafetyNet,
    GooglePlayIntegrity,
    ApplePushSecret
}

public sealed class DeviceAttestationTokenValidationRequest
{
    public DeviceAttestationTokenValidationRequest(
        DeviceAttestationTokenKind kind, string token, string? nonce = null,
        string? phoneNumber = null, string? phoneCodeHash = null)
    {
        Kind = kind;
        Token = token ?? throw new ArgumentNullException(nameof(token));
        Nonce = nonce;
        PhoneNumber = phoneNumber;
        PhoneCodeHash = phoneCodeHash;
    }

    public DeviceAttestationTokenKind Kind { get; }
    public string Token { get; }
    public string? Nonce { get; }
    public string? PhoneNumber { get; }
    public string? PhoneCodeHash { get; }

    public override string ToString() =>
        $"{nameof(DeviceAttestationTokenValidationRequest)} {{ Kind = {Kind}, Credentials = <redacted> }}";
}

public interface IDeviceAttestationTokenValidator
{
    ValueTask<bool> ValidateAsync(
        DeviceAttestationTokenValidationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class RejectingDeviceAttestationTokenValidator :
    IDeviceAttestationTokenValidator
{
    public ValueTask<bool> ValidateAsync(
        DeviceAttestationTokenValidationRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);
}
