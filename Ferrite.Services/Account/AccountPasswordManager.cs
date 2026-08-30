// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Security.Cryptography;
using System.Text;
using Ferrite.Crypto;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;
using Ferrite.TL.baseLayer.auth;
using Ferrite.TL.baseLayer.dto;
using CryptoPasswordSrpChallenge = Ferrite.Crypto.PasswordSrpChallenge;
using TLAccountPassword = Ferrite.TL.baseLayer.account.TLPassword;
using TLAuthorization = Ferrite.TL.baseLayer.auth.TLAuthorization;

namespace Ferrite.Services.Account;

public interface IAccountPasswordManager
{
    ValueTask<TLAccountPassword> GetPasswordAsync(long authKeyId);

    ValueTask<TLAuthorization> CheckPasswordAsync(long authKeyId,
        TLInputCheckPasswordSRP password);

    ValueTask<TLPasswordSettings> GetPasswordSettingsAsync(long authKeyId,
        TLInputCheckPasswordSRP password);

    ValueTask<TLBool> UpdatePasswordSettingsAsync(long authKeyId,
        TLInputCheckPasswordSRP password, TLPasswordInputSettings newSettings);

    ValueTask<RecoverySettingsApplyResult> ApplyRecoverySettingsAsync(
        long authKeyId, long userId, TLPasswordInputSettings? newSettings);

    ValueTask<TLTmpPassword> GetTemporaryPasswordAsync(long authKeyId,
        TLInputCheckPasswordSRP password, int period);

    ValueTask<PasswordVerificationStatus> VerifyPasswordAsync(long authKeyId,
        TLInputCheckPasswordSRP password);
}

public enum PasswordVerificationStatus
{
    Success,
    AuthKeyInvalid,
    PasswordMissing,
    ProofInvalid,
}

public enum RecoverySettingsApplyStatus
{
    Success,
    AuthKeyInvalid,
    PasswordMissing,
    InvalidSettings,
    PersistenceFailed,
}

public readonly record struct RecoverySettingsApplyResult(
    RecoverySettingsApplyStatus Status);

public sealed class AccountPasswordManager : IAccountPasswordManager
{
    private static readonly TimeSpan SrpChallengeTtl = TimeSpan.FromMinutes(5);
    private const int MinimumSaltLength = 8;
    private const int AddedSaltLength = 32;
    private const int TemporaryPasswordLength = 32;
    private const int MinimumTemporaryPasswordPeriod = 60;
    private const int MaximumTemporaryPasswordPeriod = 86_400;

    private readonly IAccountPasswordRepository _repository;
    private readonly IVerificationCodeRepository _verificationCodeRepository;
    private readonly IAuthorizationCompletion _authorizationCompletion;
    private readonly IVerificationCodeService _verificationCodes;
    private readonly TimeProvider _timeProvider;
    private readonly IRandomGenerator _random;

    public AccountPasswordManager(IUnitOfWork unitOfWork, IAccountPasswordRepository accountPasswordRepository, IVerificationCodeRepository verificationCodeRepository,
        IAuthorizationCompletion authorizationCompletion,
        IVerificationCodeService verificationCodes, TimeProvider timeProvider,
        IRandomGenerator random)
    {
        _repository = accountPasswordRepository;
        _verificationCodeRepository = verificationCodeRepository;
        _authorizationCompletion = authorizationCompletion;
        _verificationCodes = verificationCodes;
        _timeProvider = timeProvider;
        _random = random;
    }

    public async ValueTask<TLAccountPassword> GetPasswordAsync(long authKeyId)
    {
        PasswordPrincipal? principal = await ResolvePrincipalAsync(authKeyId,
            requireCompleted: false);
        if (principal == null)
        {
            return PasswordError("AUTH_KEY_INVALID");
        }

        TLAccountPasswordState? found = await _repository
            .GetPasswordStateAsync(principal.Value.UserId);
        using TLAccountPasswordState? state = found;
        TLPasswordResetState? foundReset = await _repository
            .GetResetStateAsync(principal.Value.UserId);
        using TLPasswordResetState? reset = foundReset;

        byte[] secureRandom = _random.GetRandomBytes(32);

        if (state is not { } current)
        {
            using var newAlgo = BuildNewPasswordAlgorithm();
            using var newSecureAlgo = BuildNewSecureAlgorithm();
            return Password.Builder()
                .NewAlgo(newAlgo.ToReadOnlySpan())
                .NewSecureAlgo(newSecureAlgo.ToReadOnlySpan())
                .SecureRandom(secureRandom)
                .Build();
        }

        var stateView = current.AsAccountPasswordState();
        if (!TryReadPasswordAlgorithm(stateView.Get_CurrentAlgoView(),
                out _, out _) ||
            !PasswordSrp.IsValidVerifier(stateView.Verifier))
        {
            return PasswordError("PASSWORD_ALGO_INVALID");
        }
        byte[] verifier = stateView.Verifier.ToArray();
        int passwordGeneration = stateView.PasswordGeneration;

        CryptoPasswordSrpChallenge srp = CreateSrpChallenge(verifier);
        long srpId = NextPositiveLong();
        int now = UnixNow();
        int expiresAt = checked(now + (int)SrpChallengeTtl.TotalSeconds);
        using TLPasswordSrpChallenge challenge =
            Ferrite.TL.baseLayer.dto.PasswordSrpChallenge.Builder()
                .SrpId(srpId)
                .AuthKeyId(authKeyId)
                .UserId(principal.Value.UserId)
                .PasswordGeneration(passwordGeneration)
                .SecretB(srp.ServerSecret)
                .CreatedAt(now)
                .ExpiresAt(expiresAt)
                .Build();
        try
        {
            await _repository.PutSrpChallengeAsync(challenge, SrpChallengeTtl);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(srp.ServerSecret);
        }

        using var newCurrentAlgo = BuildNewPasswordAlgorithm();
        using var newCurrentSecureAlgo = BuildNewSecureAlgorithm();
        var currentState = current.AsAccountPasswordState();
        var builder = Password.Builder()
            .HasPassword(true)
            .CurrentAlgo(currentState.CurrentAlgo)
            .SrpB(srp.PublicValue)
            .SrpId(srpId)
            .NewAlgo(newCurrentAlgo.ToReadOnlySpan())
            .NewSecureAlgo(newCurrentSecureAlgo.ToReadOnlySpan())
            .SecureRandom(secureRandom);
        if (currentState.Flags[1])
        {
            builder = builder.HasRecovery(true);
        }
        if (currentState.Flags[3])
        {
            builder = builder.HasSecureValues(true);
        }
        if (currentState.Flags[0])
        {
            builder = builder.Hint(currentState.Hint);
        }
        if (currentState.Flags[2])
        {
            builder = builder.EmailUnconfirmedPattern(
                Encoding.UTF8.GetBytes(MaskEmail(currentState.PendingRecoveryEmail)));
        }
        if (currentState.Flags[4])
        {
            builder = builder.LoginEmailPattern(
                Encoding.UTF8.GetBytes(MaskEmail(currentState.LoginEmail)));
        }
        if (reset is { } resetValue)
        {
            var resetState = resetValue.AsPasswordResetState();
            if (resetState.PendingUntil > now)
            {
                builder = builder.PendingResetDate(resetState.PendingUntil);
            }
        }
        return builder.Build();
    }

    public async ValueTask<TLAuthorization> CheckPasswordAsync(long authKeyId,
        TLInputCheckPasswordSRP password)
    {
        PasswordPrincipal? principal = await ResolvePrincipalAsync(authKeyId,
            requireCompleted: false);
        if (principal == null)
        {
            return AuthorizationError("AUTH_KEY_INVALID");
        }

        TLAccountPasswordState? found = await _repository
            .GetPasswordStateAsync(principal.Value.UserId);
        if (found is not { } state)
        {
            return AuthorizationError("PASSWORD_HASH_INVALID");
        }

        using (state)
        {
            PasswordProofResult proof = await VerifyProofAsync(authKeyId,
                principal.Value.UserId, state, password);
            if (proof != PasswordProofResult.Success)
            {
                return AuthorizationError("PASSWORD_HASH_INVALID");
            }
        }

        TLAuthorization? authorization = await _authorizationCompletion
            .CompleteAsync(authKeyId);
        return authorization ?? AuthorizationError("AUTH_KEY_INVALID");
    }

    public async ValueTask<PasswordVerificationStatus> VerifyPasswordAsync(
        long authKeyId, TLInputCheckPasswordSRP password)
    {
        PasswordPrincipal? principal = await ResolvePrincipalAsync(authKeyId,
            requireCompleted: true);
        if (principal == null)
        {
            return PasswordVerificationStatus.AuthKeyInvalid;
        }

        TLAccountPasswordState? found = await _repository
            .GetPasswordStateAsync(principal.Value.UserId);
        if (found is not { } state)
        {
            return PasswordVerificationStatus.PasswordMissing;
        }

        using (state)
        {
            return await VerifyProofAsync(authKeyId, principal.Value.UserId,
                       state, password) == PasswordProofResult.Success
                ? PasswordVerificationStatus.Success
                : PasswordVerificationStatus.ProofInvalid;
        }
    }

    public async ValueTask<TLPasswordSettings> GetPasswordSettingsAsync(
        long authKeyId, TLInputCheckPasswordSRP password)
    {
        PasswordPrincipal? principal = await ResolvePrincipalAsync(authKeyId,
            requireCompleted: true);
        if (principal == null)
        {
            return PasswordSettingsError("AUTH_KEY_INVALID");
        }

        TLAccountPasswordState? found = await _repository
            .GetPasswordStateAsync(principal.Value.UserId);
        if (found is not { } state)
        {
            return password.Type == TLInputCheckPasswordSRP
                    .InputCheckPasswordSRPType.InputCheckPasswordEmpty
                ? PasswordSettings.Builder().Build()
                : PasswordSettingsError("PASSWORD_HASH_INVALID");
        }

        using (state)
        {
            PasswordProofResult proof = await VerifyProofAsync(authKeyId,
                principal.Value.UserId, state, password);
            if (proof != PasswordProofResult.Success)
            {
                return PasswordSettingsError("PASSWORD_HASH_INVALID");
            }

            var current = state.AsAccountPasswordState();
            var builder = PasswordSettings.Builder();
            if (current.Flags[1])
            {
                builder = builder.Email(current.RecoveryEmail);
            }
            if (current.Flags[3])
            {
                builder = builder.SecureSettings(current.SecureSettings);
            }
            return builder.Build();
        }
    }

    public async ValueTask<TLBool> UpdatePasswordSettingsAsync(long authKeyId,
        TLInputCheckPasswordSRP password, TLPasswordInputSettings newSettings)
    {
        PasswordPrincipal? principal = await ResolvePrincipalAsync(authKeyId,
            requireCompleted: true);
        if (principal == null)
        {
            return BoolError("AUTH_KEY_INVALID");
        }

        TLAccountPasswordState? found = await _repository
            .GetPasswordStateAsync(principal.Value.UserId);
        using TLAccountPasswordState? state = found;
        if (state is { } current)
        {
            PasswordProofResult proof = await VerifyProofAsync(authKeyId,
                principal.Value.UserId, current, password);
            if (proof != PasswordProofResult.Success)
            {
                return BoolError("PASSWORD_HASH_INVALID");
            }
        }
        else if (password.Type != TLInputCheckPasswordSRP
                     .InputCheckPasswordSRPType.InputCheckPasswordEmpty)
        {
            return BoolError("PASSWORD_HASH_INVALID");
        }

        var settingsView = newSettings.AsPasswordInputSettings();
        bool changesPassword = settingsView.Flags[0];
        bool changesEmail = settingsView.Flags[1];
        bool changesSecureSettings = settingsView.Flags[2];
        bool removesPassword = changesPassword &&
                          settingsView.NewPasswordHash.IsEmpty;
        string? pendingEmail = changesEmail
            ? Encoding.UTF8.GetString(settingsView.Email)
            : null;

        if (removesPassword &&
            settingsView.Get_NewAlgoView().Type != TLPasswordKdfAlgo
                .PasswordKdfAlgoType.PasswordKdfAlgoUnknown)
        {
            return BoolError("NEW_SALT_INVALID");
        }
        if (changesPassword && !removesPassword &&
            (!TryReadNewPasswordAlgorithm(
                 settingsView.Get_NewAlgoView()) ||
             !PasswordSrp.IsValidVerifier(
                 settingsView.NewPasswordHash)))
        {
            return BoolError("NEW_SALT_INVALID");
        }

        if (state == null && !changesPassword)
        {
            return changesEmail || changesSecureSettings
                ? BoolError("PASSWORD_MISSING")
                : BoolTrue.Builder().Build();
        }

        if (removesPassword)
        {
            await _repository.DeletePasswordStateAsync(principal.Value.UserId);
            await ClearPasswordDerivedArtifactsAsync(principal.Value.UserId);
            await _verificationCodeRepository.DeleteActiveChallengeAsync(
                (int)VerificationPurpose.RecoveryEmailConfirmation, authKeyId,
                principal.Value.UserId);
            return BoolTrue.Builder().Build();
        }

        VerificationIssue? emailIssue = null;
        if (changesEmail)
        {
            if (!string.IsNullOrEmpty(pendingEmail))
            {
                if (!IsValidEmail(pendingEmail))
                {
                    return BoolError("EMAIL_INVALID");
                }
                emailIssue = await _verificationCodes.IssueEmailAsync(
                    VerificationPurpose.RecoveryEmailConfirmation, authKeyId,
                    principal.Value.UserId, pendingEmail);
            }
            else
            {
                await _verificationCodeRepository.DeleteActiveChallengeAsync(
                    (int)VerificationPurpose.RecoveryEmailConfirmation,
                    authKeyId, principal.Value.UserId);
            }
        }

        int now = UnixNow();
        var updatedSettings = newSettings.AsPasswordInputSettings();
        using TLAccountPasswordState updated = BuildUpdatedState(
            principal.Value.UserId, state, updatedSettings, changesPassword,
            pendingEmail, now);
        await _repository.PutPasswordStateAsync(updated);
        if (changesPassword)
        {
            await ClearPasswordDerivedArtifactsAsync(principal.Value.UserId);
        }

        return emailIssue is { } issue
            ? BoolError($"EMAIL_UNCONFIRMED_{issue.CodeLength}")
            : BoolTrue.Builder().Build();
    }

    public async ValueTask<TLTmpPassword> GetTemporaryPasswordAsync(
        long authKeyId, TLInputCheckPasswordSRP password, int period)
    {
        PasswordPrincipal? principal = await ResolvePrincipalAsync(authKeyId,
            requireCompleted: true);
        if (principal == null)
        {
            return TemporaryPasswordError("AUTH_KEY_INVALID");
        }
        if (period is < MinimumTemporaryPasswordPeriod or
            > MaximumTemporaryPasswordPeriod)
        {
            return TemporaryPasswordError("TMP_PASSWORD_INVALID");
        }

        TLAccountPasswordState? found = await _repository
            .GetPasswordStateAsync(principal.Value.UserId);
        if (found is not { } state)
        {
            return TemporaryPasswordError("PASSWORD_HASH_INVALID");
        }

        using (state)
        {
            PasswordProofResult proof = await VerifyProofAsync(authKeyId,
                principal.Value.UserId, state, password);
            if (proof != PasswordProofResult.Success)
            {
                return TemporaryPasswordError("PASSWORD_HASH_INVALID");
            }
        }

        byte[] token = _random.GetRandomBytes(TemporaryPasswordLength);
        byte[] digest = SHA256.HashData(token);
        int now = UnixNow();
        int validUntil = checked(now + period);
        try
        {
            using TLTemporaryPasswordState temporary = TemporaryPasswordState
                .Builder()
                .UserId(principal.Value.UserId)
                .TokenDigest(digest)
                .CreatedAt(now)
                .ValidUntil(validUntil)
                .Build();
            await _repository.PutTemporaryPasswordAsync(temporary,
                TimeSpan.FromSeconds(period));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }

        return TmpPassword.Builder()
            .TmpPasswordProperty(token)
            .ValidUntil(validUntil)
            .Build();
    }

    public async ValueTask<RecoverySettingsApplyResult>
        ApplyRecoverySettingsAsync(long authKeyId, long userId,
            TLPasswordInputSettings? newSettings)
    {
        PasswordPrincipal? principal = await ResolvePrincipalAsync(authKeyId,
            requireCompleted: false);
        if (principal == null || principal.Value.UserId != userId)
        {
            return new(RecoverySettingsApplyStatus.AuthKeyInvalid);
        }

        TLAccountPasswordState? found = await _repository
            .GetPasswordStateAsync(userId);
        if (found is not { } state)
        {
            return new(RecoverySettingsApplyStatus.PasswordMissing);
        }

        using (state)
        {
            if (newSettings == null)
            {
                try
                {
                    if (!await _repository.DeletePasswordStateAsync(userId))
                    {
                        return new(
                            RecoverySettingsApplyStatus.PersistenceFailed);
                    }
                    await ClearRecoveryArtifactsAsync(authKeyId, userId);
                    return new(RecoverySettingsApplyStatus.Success);
                }
                catch
                {
                    return new(RecoverySettingsApplyStatus.PersistenceFailed);
                }
            }

            if (newSettings.Value.Type != TLPasswordInputSettings
                    .PasswordInputSettingsType.PasswordInputSettings)
            {
                return new(RecoverySettingsApplyStatus.InvalidSettings);
            }

            var settings = newSettings.Value.AsPasswordInputSettings();
            if (!settings.Flags[0] || settings.Flags[1] ||
                settings.NewPasswordHash.IsEmpty ||
                !TryReadNewPasswordAlgorithm(
                    settings.Get_NewAlgoView()) ||
                !PasswordSrp.IsValidVerifier(settings.NewPasswordHash))
            {
                return new(RecoverySettingsApplyStatus.InvalidSettings);
            }

            TLAccountPasswordState updated = BuildRecoveredState(state.AsAccountPasswordState(),
                settings, UnixNow());

            using (updated)
            {
                try
                {
                    await _repository.PutPasswordStateAsync(updated);
                    await ClearRecoveryArtifactsAsync(authKeyId, userId);
                    return new(RecoverySettingsApplyStatus.Success);
                }
                catch
                {
                    return new(RecoverySettingsApplyStatus.PersistenceFailed);
                }
            }
        }
    }

    private async ValueTask<PasswordPrincipal?> ResolvePrincipalAsync(
        long authKeyId, bool requireCompleted)
    {
        TLAuthInfo? found = await _authorizationCompletion.ResolveAsync(authKeyId);
        if (found is not { } authorization)
        {
            return null;
        }

        using (authorization)
        {
            var info = authorization.AsAuthInfo();
            return requireCompleted && !info.LoggedIn
                ? null
                : new PasswordPrincipal(info.UserId, info.LoggedIn);
        }
    }

    private async ValueTask<PasswordProofResult> VerifyProofAsync(long authKeyId,
        long userId, TLAccountPasswordState state,
        TLInputCheckPasswordSRP password)
    {
        if (password.Type != TLInputCheckPasswordSRP
                .InputCheckPasswordSRPType.InputCheckPasswordSRP)
        {
            return PasswordProofResult.Invalid;
        }

        var input = password.AsInputCheckPasswordSRP();
        long srpId = input.SrpId;
        byte[] clientPublicValue = input.A.ToArray();
        byte[] clientProof = input.M1.ToArray();
        try
        {
            TLPasswordSrpChallenge? foundChallenge = await _repository
                .ConsumeSrpChallengeAsync(srpId);
            if (foundChallenge is not { } challenge)
            {
                return PasswordProofResult.Invalid;
            }

            using (challenge)
            {
                var challengeView = challenge.AsPasswordSrpChallenge();
                var stateView = state.AsAccountPasswordState();
                if (challengeView.AuthKeyId != authKeyId ||
                    challengeView.UserId != userId ||
                    challengeView.PasswordGeneration !=
                    stateView.PasswordGeneration ||
                    challengeView.ExpiresAt <= UnixNow() ||
                    !TryReadPasswordAlgorithm(
                        stateView.Get_CurrentAlgoView(), out byte[] salt1,
                        out byte[] salt2))
                {
                    return PasswordProofResult.Invalid;
                }

                return PasswordSrp.VerifyProof(stateView.Verifier, salt1,
                           salt2, challengeView.SecretB, clientPublicValue,
                           clientProof) == PasswordSrpVerificationResult.Success
                    ? PasswordProofResult.Success
                    : PasswordProofResult.Invalid;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clientPublicValue);
            CryptographicOperations.ZeroMemory(clientProof);
        }
    }

    private TLAccountPasswordState BuildUpdatedState(long userId,
        TLAccountPasswordState? existing, PasswordInputSettings settings,
        bool changesPassword, string? pendingEmail, int now)
    {
        var builder = AccountPasswordState.Builder()
            .UserId(userId)
            .PasswordGeneration(existing is { } generationSource
                ? generationSource.AsAccountPasswordState().PasswordGeneration +
                  (changesPassword ? 1 : 0)
                : 1)
            .CurrentAlgo(changesPassword
                ? settings.NewAlgo
                : existing!.Value.AsAccountPasswordState().CurrentAlgo)
            .Verifier(changesPassword
                ? settings.NewPasswordHash
                : existing!.Value.AsAccountPasswordState().Verifier)
            .CreatedAt(existing is { } created
                ? created.AsAccountPasswordState().CreatedAt
                : now)
            .ChangedAt(changesPassword
                ? now
                : existing!.Value.AsAccountPasswordState().ChangedAt);

        if (changesPassword)
        {
            if (!settings.Hint.IsEmpty)
            {
                builder = builder.Hint(settings.Hint);
            }
        }
        else if (existing is { } hinted &&
                 hinted.AsAccountPasswordState().Flags[0])
        {
            builder = builder.Hint(hinted.AsAccountPasswordState().Hint);
        }

        if (settings.Flags[1])
        {
            if (!string.IsNullOrEmpty(pendingEmail))
            {
                if (existing is { } confirmedEmail &&
                    confirmedEmail.AsAccountPasswordState().Flags[1])
                {
                    builder = builder.RecoveryEmail(confirmedEmail
                        .AsAccountPasswordState().RecoveryEmail);
                }
                builder = builder.PendingRecoveryEmail(
                    Encoding.UTF8.GetBytes(pendingEmail));
            }
        }
        else if (existing is { } emails)
        {
            var emailStateView = emails.AsAccountPasswordState();
            if (emailStateView.Flags[1])
            {
                builder = builder.RecoveryEmail(emailStateView.RecoveryEmail);
            }
            if (emailStateView.Flags[2])
            {
                builder = builder.PendingRecoveryEmail(
                    emailStateView.PendingRecoveryEmail);
            }
        }

        if (settings.Flags[2])
        {
            builder = builder.SecureSettings(settings.NewSecureSettings);
        }
        else if (existing is { } secured &&
                 secured.AsAccountPasswordState().Flags[3])
        {
            builder = builder.SecureSettings(
                secured.AsAccountPasswordState().SecureSettings);
        }

        if (existing is { } login)
        {
            var loginStateView = login.AsAccountPasswordState();
            if (loginStateView.Flags[4])
            {
                builder = builder.LoginEmail(loginStateView.LoginEmail);
            }
            if (loginStateView.Flags[5])
            {
                builder = builder.PendingLoginEmail(
                    loginStateView.PendingLoginEmail);
            }
        }
        return builder.Build();
    }

    private static TLAccountPasswordState BuildRecoveredState(
        AccountPasswordState current, PasswordInputSettings settings, int now)
    {
        var builder = AccountPasswordState.Builder()
            .UserId(current.UserId)
            .PasswordGeneration(checked(current.PasswordGeneration + 1))
            .CurrentAlgo(settings.NewAlgo)
            .Verifier(settings.NewPasswordHash)
            .CreatedAt(current.CreatedAt)
            .ChangedAt(now);

        if (!settings.Hint.IsEmpty)
        {
            builder = builder.Hint(settings.Hint);
        }
        if (current.Flags[1])
        {
            builder = builder.RecoveryEmail(current.RecoveryEmail);
        }
        if (settings.Flags[2])
        {
            builder = builder.SecureSettings(settings.NewSecureSettings);
        }
        if (current.Flags[4])
        {
            builder = builder.LoginEmail(current.LoginEmail);
        }
        if (current.Flags[5])
        {
            builder = builder.PendingLoginEmail(current.PendingLoginEmail);
        }
        return builder.Build();
    }

    private async ValueTask ClearRecoveryArtifactsAsync(long authKeyId,
        long userId)
    {
        await ClearPasswordDerivedArtifactsAsync(userId);
        await _verificationCodeRepository.DeleteActiveChallengeAsync(
            (int)VerificationPurpose.RecoveryEmailConfirmation, authKeyId,
            userId);
    }

    private async ValueTask ClearPasswordDerivedArtifactsAsync(long userId)
    {
        await _repository.DeleteResetStateAsync(userId);
        using TLTemporaryPasswordState? temporary = await _repository
            .ConsumeTemporaryPasswordAsync(userId);
    }

    private PasswordKdfAlgoSHA256SHA256PBKDF2HMACSHA512iter100000SHA256ModPow
        BuildNewPasswordAlgorithm() =>
        PasswordKdfAlgoSHA256SHA256PBKDF2HMACSHA512iter100000SHA256ModPow
            .Builder()
            .Salt1(_random.GetRandomBytes(32))
            .Salt2(_random.GetRandomBytes(32))
            .G(PasswordSrp.Generator)
            .P(TelegramDhParameters.Prime)
            .Build();

    private SecurePasswordKdfAlgoPBKDF2HMACSHA512iter100000
        BuildNewSecureAlgorithm() =>
        SecurePasswordKdfAlgoPBKDF2HMACSHA512iter100000.Builder()
            .Salt(_random.GetRandomBytes(32))
            .Build();

    private CryptoPasswordSrpChallenge CreateSrpChallenge(
        ReadOnlySpan<byte> verifier)
    {
        while (true)
        {
            byte[] secret = _random.GetRandomBytes(PasswordSrp.PaddedLength);
            if (PasswordSrp.TryCreateChallenge(verifier, secret,
                    out CryptoPasswordSrpChallenge challenge))
            {
                CryptographicOperations.ZeroMemory(secret);
                return challenge;
            }
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private static bool TryReadPasswordAlgorithm(PasswordKdfAlgoView view,
        out byte[] salt1, out byte[] salt2)
    {
        salt1 = Array.Empty<byte>();
        salt2 = Array.Empty<byte>();
        if (!view.Is(out
                PasswordKdfAlgoSHA256SHA256PBKDF2HMACSHA512iter100000SHA256ModPow
                algorithm) ||
            algorithm.G != PasswordSrp.Generator ||
            !algorithm.P.SequenceEqual(TelegramDhParameters.Prime) ||
            algorithm.Salt1.Length < MinimumSaltLength ||
            algorithm.Salt2.Length < MinimumSaltLength)
        {
            return false;
        }
        salt1 = algorithm.Salt1.ToArray();
        salt2 = algorithm.Salt2.ToArray();
        return true;
    }

    private static bool TryReadNewPasswordAlgorithm(PasswordKdfAlgoView view)
    {
        return view.Is(out
                   PasswordKdfAlgoSHA256SHA256PBKDF2HMACSHA512iter100000SHA256ModPow
                   algorithm) &&
               algorithm.G == PasswordSrp.Generator &&
               algorithm.P.SequenceEqual(TelegramDhParameters.Prime) &&
               algorithm.Salt1.Length >= MinimumSaltLength + AddedSaltLength &&
               algorithm.Salt2.Length >= MinimumSaltLength;
    }

    private long NextPositiveLong()
    {
        long result = _random.NextLong() & long.MaxValue;
        return result == 0 ? 1 : result;
    }

    private int UnixNow() =>
        checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());

    private static bool IsValidEmail(string email)
    {
        int at = email.IndexOf('@');
        return at > 0 && at == email.LastIndexOf('@') &&
               at < email.Length - 1 && email.Length <= 254;
    }

    private static string MaskEmail(ReadOnlySpan<byte> emailBytes)
    {
        string email = Encoding.UTF8.GetString(emailBytes);
        int at = email.IndexOf('@');
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }

    private static TLAccountPassword PasswordError(string message) =>
        (TLAccountPassword)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    private static TLAuthorization AuthorizationError(string message) =>
        (TLAuthorization)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    private static TLPasswordSettings PasswordSettingsError(string message) =>
        (TLPasswordSettings)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    private static TLBool BoolError(string message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    private static TLTmpPassword TemporaryPasswordError(string message) =>
        (TLTmpPassword)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    private readonly record struct PasswordPrincipal(long UserId, bool LoggedIn);

    private enum PasswordProofResult
    {
        Invalid,
        Success,
    }
}
