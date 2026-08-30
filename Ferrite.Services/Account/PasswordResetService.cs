// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Account;

public interface IPasswordResetService
{
    ValueTask<TLResetPasswordResult> ResetAsync(long authKeyId,
        CancellationToken cancellationToken = default);

    ValueTask<TLBool> DeclineAsync(long authKeyId,
        CancellationToken cancellationToken = default);
}

public sealed class PasswordResetService : IPasswordResetService
{
    private readonly IAccountPasswordRepository _accountPasswordRepository;
    private readonly IVerificationCodeRepository _verificationCodeRepository;

    private const int PendingState = 1;
    private const int DeclinedState = 2;
    private static readonly TimeSpan ResetDelay = TimeSpan.FromDays(7);
    private static readonly TimeSpan DeclineCooldown = TimeSpan.FromDays(7);

    private readonly IAccountPasswordRepository _passwords;
    private readonly IVerificationCodeRepository _verificationCodes;
    private readonly IAuthorizationCompletion _authorizationCompletion;
    private readonly TimeProvider _timeProvider;

    public PasswordResetService(IUnitOfWork unitOfWork, IAccountPasswordRepository accountPasswordRepository, IVerificationCodeRepository verificationCodeRepository,
        IAuthorizationCompletion authorizationCompletion,
        TimeProvider timeProvider)
    {
        _accountPasswordRepository = accountPasswordRepository;
        _verificationCodeRepository = verificationCodeRepository;

        _passwords = accountPasswordRepository;
        _verificationCodes = verificationCodeRepository;
        _authorizationCompletion = authorizationCompletion;
        _timeProvider = timeProvider;
    }

    public async ValueTask<TLResetPasswordResult> ResetAsync(long authKeyId,
        CancellationToken cancellationToken = default)
    {
        long? userId = await ResolveAuthorizedUserAsync(authKeyId);
        if (userId == null)
        {
            return ResetError("AUTH_KEY_INVALID");
        }

        TLAccountPasswordState? password = await _passwords
            .GetPasswordStateAsync(userId.Value, cancellationToken);
        using TLAccountPasswordState? passwordState = password;
        if (passwordState == null)
        {
            await ClearPasswordArtifactsAsync(userId.Value, authKeyId,
                cancellationToken);
            await _passwords.DeleteResetStateAsync(userId.Value,
                cancellationToken);
            return ResetPasswordOk.Builder().Build();
        }

        int now = UnixNow();
        TLPasswordResetState? found = await _passwords.GetResetStateAsync(
            userId.Value, cancellationToken);
        using TLPasswordResetState? state = found;
        if (state is { } current)
        {
            var view = current.AsPasswordResetState();
            if (view.State == PendingState)
            {
                if (view.PendingUntil > now)
                {
                    return ResetPasswordRequestedWait.Builder()
                        .UntilDate(view.PendingUntil)
                        .Build();
                }

                await _passwords.DeletePasswordStateAsync(userId.Value,
                    cancellationToken);
                await ClearPasswordArtifactsAsync(userId.Value, authKeyId,
                    cancellationToken);
                await _passwords.DeleteResetStateAsync(userId.Value,
                    cancellationToken);
                return ResetPasswordOk.Builder().Build();
            }

            if (view.State == DeclinedState && view.RetryAt > now)
            {
                return ResetPasswordFailedWait.Builder()
                    .RetryDate(view.RetryAt)
                    .Build();
            }
        }

        int pendingUntil = checked(now + (int)ResetDelay.TotalSeconds);
        using TLPasswordResetState requested = PasswordResetState.Builder()
            .UserId(userId.Value)
            .State(PendingState)
            .RequestedAt(now)
            .PendingUntil(pendingUntil)
            .RetryAt(0)
            .Build();
        await _passwords.PutResetStateAsync(requested, cancellationToken);
        return ResetPasswordRequestedWait.Builder()
            .UntilDate(pendingUntil)
            .Build();
    }

    public async ValueTask<TLBool> DeclineAsync(long authKeyId,
        CancellationToken cancellationToken = default)
    {
        long? userId = await ResolveAuthorizedUserAsync(authKeyId);
        if (userId == null)
        {
            return BoolError("AUTH_KEY_INVALID");
        }

        TLPasswordResetState? found = await _passwords.GetResetStateAsync(
            userId.Value, cancellationToken);
        if (found is not { } state)
        {
            return BoolError("RESET_REQUEST_MISSING");
        }

        int now = UnixNow();
        using (state)
        {
            var view = state.AsPasswordResetState();
            if (view.State != PendingState || view.PendingUntil <= now)
            {
                return BoolError("RESET_REQUEST_MISSING");
            }
        }

        int retryAt = checked(now + (int)DeclineCooldown.TotalSeconds);
        using TLPasswordResetState declined = PasswordResetState.Builder()
            .UserId(userId.Value)
            .State(DeclinedState)
            .RequestedAt(now)
            .PendingUntil(0)
            .RetryAt(retryAt)
            .Build();
        await _passwords.PutResetStateAsync(declined, cancellationToken);
        return BoolTrue.Builder().Build();
    }

    private async ValueTask<long?> ResolveAuthorizedUserAsync(long authKeyId)
    {
        TLAuthInfo? found = await _authorizationCompletion.ResolveAsync(authKeyId);
        if (found is not { } authorization)
        {
            return null;
        }

        using (authorization)
        {
            var view = authorization.AsAuthInfo();
            return view.LoggedIn ? view.UserId : null;
        }
    }

    private int UnixNow() =>
        checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());

    private async ValueTask ClearPasswordArtifactsAsync(long userId,
        long authKeyId, CancellationToken cancellationToken)
    {
        using TLTemporaryPasswordState? temporary = await _passwords
            .ConsumeTemporaryPasswordAsync(userId, cancellationToken);
        await _verificationCodes.DeleteActiveChallengeAsync(
            (int)VerificationPurpose.RecoveryEmailConfirmation, authKeyId,
            userId, cancellationToken);
    }

    private static TLResetPasswordResult ResetError(string message) =>
        (TLResetPasswordResult)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    private static TLBool BoolError(string message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
