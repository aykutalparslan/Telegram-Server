// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Gateway;

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

public interface IWebAuthorizationTokenValidator
{
    ValueTask<WebAuthorizationTokenValidationResult> ValidateAsync(
        WebAuthorizationTokenValidationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class RejectingWebAuthorizationTokenValidator :
    IWebAuthorizationTokenValidator
{
    public ValueTask<WebAuthorizationTokenValidationResult> ValidateAsync(
        WebAuthorizationTokenValidationRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(WebAuthorizationTokenValidationResult.Rejected);
}
