// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer.auth;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers;

internal static class VerificationHandlerSupport
{
    private const int PhoneCodeTimeout = 60;

    public readonly record struct AuthenticatedUser(long UserId, string Phone);

    public static async ValueTask<long?> GetLoggedInUserIdAsync(
        IAuthorizationRepository authorizationRepository, long authKeyId)
    {
        AuthenticatedUser? user = await GetLoggedInUserAsync(authorizationRepository,
            authKeyId);
        return user?.UserId;
    }

    public static async ValueTask<AuthenticatedUser?> GetLoggedInUserAsync(
        IAuthorizationRepository authorizationRepository, long authKeyId)
    {
        TLAuthInfo? found = await authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (found is not { } authorization)
        {
            return null;
        }

        using (authorization)
        {
            AuthInfo row = authorization.AsAuthInfo();
            return row.LoggedIn
                ? new AuthenticatedUser(row.UserId,
                    Encoding.UTF8.GetString(row.Phone))
                : null;
        }
    }

    public static TLSentCode BuildSentCode(VerificationIssue issue)
    {
        using var type = new SentCodeTypeSms(issue.CodeLength);
        return SentCode.Builder()
            .Type(type.ToReadOnlySpan())
            .PhoneCodeHash(Encoding.UTF8.GetBytes(issue.PublicHash))
            .Timeout(PhoneCodeTimeout)
            .Build();
    }

    public static string MaskEmail(string email)
    {
        int at = email.IndexOf('@');
        if (at <= 0 || at == email.Length - 1)
        {
            return "***";
        }
        return $"{email[0]}***{email[at..]}";
    }
}
