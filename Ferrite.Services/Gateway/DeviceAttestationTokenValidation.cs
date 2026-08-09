// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Gateway;

public enum DeviceAttestationTokenKind
{
    SafetyNet,
    GooglePlayIntegrity,
    ApplePushSecret
}

/// <summary>
/// An untrusted device-attestation token and the request it is bound to.
/// </summary>
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

/// <summary>
/// Validates Firebase, device-integrity, and APNs proof tokens.
/// </summary>
public interface IDeviceAttestationTokenValidator
{
    ValueTask<bool> ValidateAsync(
        DeviceAttestationTokenValidationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Safe production default used until a deployment configures a trusted issuer.
/// </summary>
public sealed class RejectingDeviceAttestationTokenValidator :
    IDeviceAttestationTokenValidator
{
    public ValueTask<bool> ValidateAsync(
        DeviceAttestationTokenValidationRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);
}
