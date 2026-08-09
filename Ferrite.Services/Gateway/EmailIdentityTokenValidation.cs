// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Gateway;

public enum EmailIdentityTokenProvider
{
    Google,
    Apple
}

/// <summary>
/// An untrusted identity token supplied instead of an emailed verification code.
/// </summary>
public sealed class EmailIdentityTokenValidationRequest
{
    public EmailIdentityTokenValidationRequest(
        EmailIdentityTokenProvider provider, string token)
    {
        Provider = provider;
        Token = token ?? throw new ArgumentNullException(nameof(token));
    }

    public EmailIdentityTokenProvider Provider { get; }
    public string Token { get; }

    public override string ToString() =>
        $"{nameof(EmailIdentityTokenValidationRequest)} {{ Provider = {Provider}, Token = <redacted> }}";
}

/// <summary>
/// The email address proven by a trusted Apple or Google identity token.
/// </summary>
public readonly struct EmailIdentityTokenValidationResult
{
    private EmailIdentityTokenValidationResult(bool isValid, string? email)
    {
        IsValid = isValid;
        Email = email;
    }

    public bool IsValid { get; }
    public string? Email { get; }

    public static EmailIdentityTokenValidationResult Rejected => default;

    public static EmailIdentityTokenValidationResult Accepted(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return new EmailIdentityTokenValidationResult(true, email);
    }
}

/// <summary>
/// Validates Apple and Google email identity tokens at the external trust boundary.
/// </summary>
public interface IEmailIdentityTokenValidator
{
    ValueTask<EmailIdentityTokenValidationResult> ValidateAsync(
        EmailIdentityTokenValidationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Safe production default used until a deployment configures a trusted issuer.
/// </summary>
public sealed class RejectingEmailIdentityTokenValidator :
    IEmailIdentityTokenValidator
{
    public ValueTask<EmailIdentityTokenValidationResult> ValidateAsync(
        EmailIdentityTokenValidationRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(EmailIdentityTokenValidationResult.Rejected);
}
