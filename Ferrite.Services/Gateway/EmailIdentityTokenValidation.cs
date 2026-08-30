// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Gateway;

public enum EmailIdentityTokenProvider
{
    Google,
    Apple
}

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

public interface IEmailIdentityTokenValidator
{
    ValueTask<EmailIdentityTokenValidationResult> ValidateAsync(
        EmailIdentityTokenValidationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class RejectingEmailIdentityTokenValidator :
    IEmailIdentityTokenValidator
{
    public ValueTask<EmailIdentityTokenValidationResult> ValidateAsync(
        EmailIdentityTokenValidationRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(EmailIdentityTokenValidationResult.Rejected);
}
