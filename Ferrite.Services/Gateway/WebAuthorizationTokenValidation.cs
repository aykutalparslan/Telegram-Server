// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Gateway;

/// <summary>
/// The untrusted inputs supplied to <c>auth.importWebTokenAuthorization</c>.
/// </summary>
public sealed class WebAuthorizationTokenValidationRequest
{
    public WebAuthorizationTokenValidationRequest(int apiId, string apiHash,
        string token)
    {
        ApiId = apiId;
        ApiHash = apiHash ?? throw new ArgumentNullException(nameof(apiHash));
        Token = token ?? throw new ArgumentNullException(nameof(token));
    }

    public int ApiId { get; }
    public string ApiHash { get; }
    public string Token { get; }

    public override string ToString() =>
        $"{nameof(WebAuthorizationTokenValidationRequest)} {{ ApiId = {ApiId}, Credentials = <redacted> }}";
}

/// <summary>
/// The local identity proven by an external web authorization token.
/// </summary>
public readonly struct WebAuthorizationTokenValidationResult
{
    private WebAuthorizationTokenValidationResult(bool isValid, long userId)
    {
        IsValid = isValid;
        UserId = userId;
    }

    public bool IsValid { get; }
    public long UserId { get; }

    public static WebAuthorizationTokenValidationResult Rejected => default;

    public static WebAuthorizationTokenValidationResult Accepted(long userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        return new WebAuthorizationTokenValidationResult(true, userId);
    }
}

/// <summary>
/// Validates web authorization tokens at the external trust boundary.
/// </summary>
public interface IWebAuthorizationTokenValidator
{
    ValueTask<WebAuthorizationTokenValidationResult> ValidateAsync(
        WebAuthorizationTokenValidationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Safe production default used until a deployment configures a trusted issuer.
/// </summary>
public sealed class RejectingWebAuthorizationTokenValidator :
    IWebAuthorizationTokenValidator
{
    public ValueTask<WebAuthorizationTokenValidationResult> ValidateAsync(
        WebAuthorizationTokenValidationRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(WebAuthorizationTokenValidationResult.Rejected);
}
