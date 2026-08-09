// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;
using Ferrite.TL.baseLayer.auth;
using Ferrite.TL.baseLayer.dto;
using TLAuthorization = Ferrite.TL.baseLayer.auth.TLAuthorization;

namespace Ferrite.Services;

public interface IPasswordRecoveryService
{
    ValueTask<TLPasswordRecovery> RequestAsync(long authKeyId,
        CancellationToken cancellationToken = default);

    ValueTask<TLBool> CheckAsync(long authKeyId, string code,
        CancellationToken cancellationToken = default);

    ValueTask<TLAuthorization> RecoverAsync(long authKeyId, string code,
        TLPasswordInputSettings? newSettings,
        CancellationToken cancellationToken = default);

    ValueTask<TLBool> ConfirmEmailAsync(long authKeyId, string code,
        CancellationToken cancellationToken = default);

    ValueTask<TLBool> ResendEmailAsync(long authKeyId,
        CancellationToken cancellationToken = default);

    ValueTask<TLBool> CancelEmailAsync(long authKeyId,
        CancellationToken cancellationToken = default);
}

public sealed class PasswordRecoveryService : IPasswordRecoveryService
{
    private readonly IAccountPasswordRepository _passwords;
    private readonly IVerificationCodeRepository _verificationCodeRepository;
    private readonly IVerificationCodeService _verificationCodes;
    private readonly IAuthorizationCompletion _authorizationCompletion;
    private readonly IAccountPasswordManager _accountPasswords;

    public PasswordRecoveryService(IAccountPasswordRepository accountPasswordRepository,
        IVerificationCodeRepository verificationCodeRepository,
        IVerificationCodeService verificationCodes,
        IAuthorizationCompletion authorizationCompletion,
        IAccountPasswordManager accountPasswords)
    {
        _passwords = accountPasswordRepository;
        _verificationCodeRepository = verificationCodeRepository;
        _verificationCodes = verificationCodes;
        _authorizationCompletion = authorizationCompletion;
        _accountPasswords = accountPasswords;
    }

    public async ValueTask<TLPasswordRecovery> RequestAsync(long authKeyId,
        CancellationToken cancellationToken = default)
    {
        RecoveryPrincipal? principal = await ResolvePrincipalAsync(authKeyId,
            requireCompleted: false);
        if (principal == null)
        {
            return PasswordRecoveryError("AUTH_KEY_INVALID");
        }

        TLAccountPasswordState? found = await _passwords
            .GetPasswordStateAsync(principal.Value.UserId, cancellationToken);
        if (found is not { } state)
        {
            return PasswordRecoveryError("PASSWORD_RECOVERY_NA");
        }

        string email;
        using (state)
        {
            var view = state.AsAccountPasswordState();
            if (!view.HasPassword || !view.Flags[1] ||
                view.RecoveryEmail.IsEmpty)
            {
                return PasswordRecoveryError("PASSWORD_RECOVERY_NA");
            }
            email = Encoding.UTF8.GetString(view.RecoveryEmail);
        }

        await _verificationCodes.IssueEmailAsync(
            VerificationPurpose.PasswordRecovery, authKeyId,
            principal.Value.UserId, email, cancellationToken:
            cancellationToken);
        return PasswordRecovery.Builder()
            .EmailPattern(Encoding.UTF8.GetBytes(MaskEmail(email)))
            .Build();
    }

    public async ValueTask<TLBool> CheckAsync(long authKeyId, string code,
        CancellationToken cancellationToken = default)
    {
        RecoveryPrincipal? principal = await ResolvePrincipalAsync(authKeyId,
            requireCompleted: false);
        if (principal == null)
        {
            return BoolError("AUTH_KEY_INVALID");
        }
        if (!await HasRecoveryEmailAsync(principal.Value.UserId,
                cancellationToken))
        {
            return BoolError("PASSWORD_RECOVERY_NA");
        }

        VerifiedChallenge? verified = await _verificationCodes
            .VerifyActiveAsync(VerificationPurpose.PasswordRecovery,
                authKeyId, principal.Value.UserId, code, consume: false,
                cancellationToken);
        return verified == null
            ? BoolError("CODE_INVALID")
            : BoolTrue.Builder().Build();
    }

    public async ValueTask<TLAuthorization> RecoverAsync(long authKeyId,
        string code, TLPasswordInputSettings? newSettings,
        CancellationToken cancellationToken = default)
    {
        RecoveryPrincipal? principal = await ResolvePrincipalAsync(authKeyId,
            requireCompleted: false);
        if (principal == null)
        {
            return AuthorizationError("AUTH_KEY_INVALID");
        }

        string? recoveryEmail = await GetRecoveryEmailAsync(
            principal.Value.UserId, cancellationToken);
        if (recoveryEmail == null)
        {
            return AuthorizationError("PASSWORD_RECOVERY_NA");
        }

        VerifiedChallenge? verified = await _verificationCodes
            .VerifyActiveAsync(VerificationPurpose.PasswordRecovery,
                authKeyId, principal.Value.UserId, code, consume: true,
                cancellationToken);
        if (verified == null || !StringComparer.OrdinalIgnoreCase.Equals(
                verified.Value.Destination, recoveryEmail))
        {
            return AuthorizationError("CODE_INVALID");
        }

        RecoverySettingsApplyResult applied = await _accountPasswords
            .ApplyRecoverySettingsAsync(authKeyId, principal.Value.UserId,
                newSettings);
        if (applied.Status != RecoverySettingsApplyStatus.Success)
        {
            return applied.Status switch
            {
                RecoverySettingsApplyStatus.AuthKeyInvalid =>
                    AuthorizationError("AUTH_KEY_INVALID"),
                RecoverySettingsApplyStatus.PasswordMissing =>
                    AuthorizationError("PASSWORD_RECOVERY_NA"),
                RecoverySettingsApplyStatus.InvalidSettings =>
                    AuthorizationError("NEW_SALT_INVALID"),
                _ => AuthorizationError("INTERNAL_SERVER_ERROR", 500),
            };
        }

        TLAuthorization? authorization = await _authorizationCompletion
            .CompleteAsync(authKeyId);
        return authorization ?? AuthorizationError("AUTH_KEY_INVALID");
    }

    public async ValueTask<TLBool> ConfirmEmailAsync(long authKeyId,
        string code, CancellationToken cancellationToken = default)
    {
        RecoveryPrincipal? principal = await ResolvePrincipalAsync(authKeyId,
            requireCompleted: true);
        if (principal == null)
        {
            return BoolError("AUTH_KEY_INVALID");
        }

        TLAccountPasswordState? found = await _passwords
            .GetPasswordStateAsync(principal.Value.UserId, cancellationToken);
        if (found is not { } state)
        {
            return BoolError("EMAIL_HASH_EXPIRED");
        }

        using (state)
        {
            var view = state.AsAccountPasswordState();
            if (!view.Flags[2] || view.PendingRecoveryEmail.IsEmpty)
            {
                return BoolError("EMAIL_HASH_EXPIRED");
            }
            string pendingEmail = Encoding.UTF8.GetString(
                view.PendingRecoveryEmail);

            VerifiedChallenge? verified = await _verificationCodes
                .VerifyActiveAsync(
                    VerificationPurpose.RecoveryEmailConfirmation,
                    authKeyId, principal.Value.UserId, code, consume: true,
                    cancellationToken);
            if (verified == null || !StringComparer.OrdinalIgnoreCase.Equals(
                    verified.Value.Destination, pendingEmail))
            {
                return BoolError("CODE_INVALID");
            }

            using TLAccountPasswordState confirmed = RebuildEmailState(
                state.AsAccountPasswordState(),
                Encoding.UTF8.GetBytes(pendingEmail));
            await _passwords.PutPasswordStateAsync(confirmed,
                cancellationToken);
            return BoolTrue.Builder().Build();
        }
    }

    public async ValueTask<TLBool> ResendEmailAsync(long authKeyId,
        CancellationToken cancellationToken = default)
    {
        RecoveryPrincipal? principal = await ResolvePrincipalAsync(authKeyId,
            requireCompleted: true);
        if (principal == null)
        {
            return BoolError("AUTH_KEY_INVALID");
        }

        string? pendingEmail = await GetPendingRecoveryEmailAsync(
            principal.Value.UserId, cancellationToken);
        if (pendingEmail == null)
        {
            return BoolError("EMAIL_HASH_EXPIRED");
        }

        TLVerificationChallenge? found = await _verificationCodeRepository
            .GetActiveChallengeAsync(
                (int)VerificationPurpose.RecoveryEmailConfirmation,
                authKeyId, principal.Value.UserId, cancellationToken);
        if (found is not { } challenge)
        {
            return BoolError("EMAIL_HASH_EXPIRED");
        }

        string publicHash;
        using (challenge)
        {
            var view = challenge.AsVerificationChallenge();
            if (!StringComparer.OrdinalIgnoreCase.Equals(
                    Encoding.UTF8.GetString(view.Destination), pendingEmail))
            {
                return BoolError("EMAIL_HASH_EXPIRED");
            }
            publicHash = Encoding.UTF8.GetString(view.PublicHash);
        }

        VerificationIssue? issue = await _verificationCodes.ResendAsync(
            VerificationPurpose.RecoveryEmailConfirmation, authKeyId,
            principal.Value.UserId, publicHash, cancellationToken);
        return issue == null
            ? BoolError("EMAIL_HASH_EXPIRED")
            : BoolTrue.Builder().Build();
    }

    public async ValueTask<TLBool> CancelEmailAsync(long authKeyId,
        CancellationToken cancellationToken = default)
    {
        RecoveryPrincipal? principal = await ResolvePrincipalAsync(authKeyId,
            requireCompleted: true);
        if (principal == null)
        {
            return BoolError("AUTH_KEY_INVALID");
        }

        TLAccountPasswordState? found = await _passwords
            .GetPasswordStateAsync(principal.Value.UserId, cancellationToken);
        if (found is not { } state)
        {
            return BoolError("EMAIL_HASH_EXPIRED");
        }

        using (state)
        {
            var current = state.AsAccountPasswordState();
            if (!current.Flags[2])
            {
                return BoolError("EMAIL_HASH_EXPIRED");
            }

            byte[] recoveryEmail = current.Flags[1]
                ? current.RecoveryEmail.ToArray()
                : Array.Empty<byte>();
            using TLAccountPasswordState cancelled = RebuildEmailState(current,
                recoveryEmail);
            await _passwords.PutPasswordStateAsync(cancelled,
                cancellationToken);
        }

        await _verificationCodeRepository.DeleteActiveChallengeAsync(
            (int)VerificationPurpose.RecoveryEmailConfirmation, authKeyId,
            principal.Value.UserId, cancellationToken);
        return BoolTrue.Builder().Build();
    }

    private async ValueTask<RecoveryPrincipal?> ResolvePrincipalAsync(
        long authKeyId, bool requireCompleted)
    {
        TLAuthInfo? found = await _authorizationCompletion.ResolveAsync(
            authKeyId);
        if (found is not { } authorization)
        {
            return null;
        }

        using (authorization)
        {
            var view = authorization.AsAuthInfo();
            return requireCompleted && !view.LoggedIn
                ? null
                : new RecoveryPrincipal(view.UserId);
        }
    }

    private async ValueTask<bool> HasRecoveryEmailAsync(long userId,
        CancellationToken cancellationToken)
    {
        return await GetRecoveryEmailAsync(userId, cancellationToken) != null;
    }

    private async ValueTask<string?> GetRecoveryEmailAsync(long userId,
        CancellationToken cancellationToken)
    {
        TLAccountPasswordState? found = await _passwords
            .GetPasswordStateAsync(userId, cancellationToken);
        if (found is not { } state)
        {
            return null;
        }

        using (state)
        {
            var view = state.AsAccountPasswordState();
            return view.HasPassword && view.Flags[1] &&
                   !view.RecoveryEmail.IsEmpty
                ? Encoding.UTF8.GetString(view.RecoveryEmail)
                : null;
        }
    }

    private async ValueTask<string?> GetPendingRecoveryEmailAsync(long userId,
        CancellationToken cancellationToken)
    {
        TLAccountPasswordState? found = await _passwords
            .GetPasswordStateAsync(userId, cancellationToken);
        if (found is not { } state)
        {
            return null;
        }

        using (state)
        {
            var view = state.AsAccountPasswordState();
            return view.HasPassword && view.Flags[2] &&
                   !view.PendingRecoveryEmail.IsEmpty
                ? Encoding.UTF8.GetString(view.PendingRecoveryEmail)
                : null;
        }
    }

    private static TLAccountPasswordState RebuildEmailState(
        AccountPasswordState current, ReadOnlySpan<byte> recoveryEmail)
    {
        var builder = AccountPasswordState.Builder()
            .UserId(current.UserId)
            .PasswordGeneration(current.PasswordGeneration)
            .CreatedAt(current.CreatedAt)
            .ChangedAt(current.ChangedAt);
        if (current.Flags[0])
        {
            builder = builder.Hint(current.Hint);
        }
        if (!recoveryEmail.IsEmpty)
        {
            builder = builder.RecoveryEmail(recoveryEmail);
        }
        if (current.Flags[3])
        {
            builder = builder.SecureSettings(current.SecureSettings);
        }
        if (current.Flags[4])
        {
            builder = builder.LoginEmail(current.LoginEmail);
        }
        if (current.Flags[5])
        {
            builder = builder.PendingLoginEmail(current.PendingLoginEmail);
        }
        if (current.HasPassword)
        {
            builder = builder.CurrentAlgo(current.CurrentAlgo)
                .Verifier(current.Verifier);
        }
        return builder.Build();
    }

    private static string MaskEmail(string email)
    {
        int at = email.IndexOf('@');
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }

    private static TLPasswordRecovery PasswordRecoveryError(string message) =>
        (TLPasswordRecovery)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    private static TLAuthorization AuthorizationError(string message,
        int code = 400) =>
        (TLAuthorization)RpcErrorGenerator.GenerateError(code,
            Encoding.UTF8.GetBytes(message));

    private static TLBool BoolError(string message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    private readonly record struct RecoveryPrincipal(long UserId);
}
