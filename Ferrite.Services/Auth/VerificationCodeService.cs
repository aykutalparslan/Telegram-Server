// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Security.Cryptography;
using System.Text;
using Ferrite.Crypto;
using Ferrite.Data.Repositories;
using Ferrite.Services.Gateway;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Auth;

public enum VerificationPurpose
{
    LoginPhone = 1,
    FirebaseLogin = 2,
    LoginEmailSetup = 3,
    LoginEmailChange = 4,
    PasswordRecovery = 5,
    RecoveryEmailConfirmation = 6,
    VerifyPhone = 7,
    VerifyEmail = 8,
    ConfirmPhone = 9,
}

public enum VerificationChannel
{
    Sms = 1,
    Email = 2,
}

public readonly record struct VerificationIssue(string PublicHash,
    int CodeLength, int ExpiresAt);

public readonly record struct VerifiedChallenge(long ChallengeId,
    VerificationPurpose Purpose, long AuthKeyId, long SubjectId,
    string Destination, byte[] Context);

public interface IVerificationCodeService
{
    ValueTask<VerificationIssue> IssueSmsAsync(VerificationPurpose purpose,
        long authKeyId, long subjectId, string phone,
        ReadOnlyMemory<byte> context = default,
        CancellationToken cancellationToken = default);

    ValueTask<VerificationIssue> IssueEmailAsync(VerificationPurpose purpose,
        long authKeyId, long subjectId, string email,
        ReadOnlyMemory<byte> context = default,
        CancellationToken cancellationToken = default);

    ValueTask<VerifiedChallenge?> VerifyAsync(VerificationPurpose purpose,
        long authKeyId, long subjectId, string publicHash, string code,
        bool consume = true, CancellationToken cancellationToken = default);

    ValueTask<VerifiedChallenge?> GetActiveAsync(VerificationPurpose purpose,
        long authKeyId, long subjectId, string expectedPublicHash,
        CancellationToken cancellationToken = default);

    ValueTask<VerifiedChallenge?> VerifyActiveAsync(
        VerificationPurpose purpose, long authKeyId, long subjectId,
        string code, bool consume = true,
        CancellationToken cancellationToken = default);

    ValueTask<bool> ReissueSmsInPlaceAsync(VerificationPurpose purpose,
        long authKeyId, long subjectId, string expectedPublicHash,
        CancellationToken cancellationToken = default);

    ValueTask<VerificationIssue?> ResendAsync(VerificationPurpose purpose,
        long authKeyId, long subjectId, string expectedPublicHash,
        CancellationToken cancellationToken = default);

    ValueTask<bool> CancelAsync(VerificationPurpose purpose, long authKeyId,
        long subjectId, string expectedPublicHash,
        CancellationToken cancellationToken = default);

    ValueTask<bool> ReportMissingAsync(VerificationPurpose purpose,
        long authKeyId, long subjectId, string publicHash,
        CancellationToken cancellationToken = default);

    ValueTask<int> InvalidateByCodesAsync(IEnumerable<string> codes,
        CancellationToken cancellationToken = default);
}

public sealed class VerificationCodeService : IVerificationCodeService
{
    private readonly IVerificationCodeRepository _verificationCodeRepository;

    private static readonly TimeSpan ChallengeTtl = TimeSpan.FromMinutes(5);

    private readonly IVerificationCodeRepository _repository;
    private readonly IVerificationGateway _gateway;
    private readonly IRandomGenerator _random;
    private readonly TimeProvider _timeProvider;

    public VerificationCodeService(IUnitOfWork unitOfWork, IVerificationCodeRepository verificationCodeRepository,
        IVerificationGateway gateway, IRandomGenerator random,
        TimeProvider timeProvider)
    {
        _verificationCodeRepository = verificationCodeRepository;

        _repository = verificationCodeRepository;
        _gateway = gateway;
        _random = random;
        _timeProvider = timeProvider;
    }

    public async ValueTask<VerificationIssue> IssueSmsAsync(
        VerificationPurpose purpose, long authKeyId, long subjectId,
        string phone, ReadOnlyMemory<byte> context = default,
        CancellationToken cancellationToken = default)
    {
        string code = await _gateway.SendSms(phone);
        return await PutAsync(purpose, VerificationChannel.Sms, authKeyId,
            subjectId, phone, code, context, cancellationToken);
    }

    public async ValueTask<VerificationIssue> IssueEmailAsync(
        VerificationPurpose purpose, long authKeyId, long subjectId,
        string email, ReadOnlyMemory<byte> context = default,
        CancellationToken cancellationToken = default)
    {
        string code = await _gateway.SendEmail(email);
        return await PutAsync(purpose, VerificationChannel.Email, authKeyId,
            subjectId, email, code, context, cancellationToken);
    }

    public async ValueTask<VerifiedChallenge?> VerifyAsync(
        VerificationPurpose purpose, long authKeyId, long subjectId,
        string publicHash, string code, bool consume = true,
        CancellationToken cancellationToken = default)
    {
        byte[] suppliedDigest = Digest(code);
        try
        {
            TLVerificationChallenge? found = await _repository
                .GetChallengeAsync(publicHash, cancellationToken);
            if (found is not { } challenge)
            {
                return null;
            }

            VerifiedChallenge verified;
            using (challenge)
            {
                var view = challenge.AsVerificationChallenge();
                if (!Matches(view, purpose, authKeyId, subjectId,
                        suppliedDigest, UnixNow()))
                {
                    return null;
                }

                verified = ToVerifiedChallenge(view);
            }

            if (!consume)
            {
                return verified;
            }

            TLVerificationChallenge? consumed = await _repository
                .ConsumeChallengeAsync(publicHash, cancellationToken);
            if (consumed is not { } consumedValue)
            {
                return null;
            }
            using (consumedValue)
            {
                var consumedView = consumedValue.AsVerificationChallenge();
                return consumedView.ChallengeId == verified.ChallengeId &&
                       Matches(consumedView, purpose, authKeyId, subjectId,
                           suppliedDigest, UnixNow())
                    ? ToVerifiedChallenge(consumedView)
                    : null;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(suppliedDigest);
        }
    }

    private static bool Matches(VerificationChallenge challenge,
        VerificationPurpose purpose, long authKeyId, long subjectId,
        ReadOnlySpan<byte> suppliedDigest, int now) =>
        challenge.Purpose == (int)purpose &&
        challenge.AuthKeyId == authKeyId &&
        challenge.SubjectId == subjectId &&
        challenge.ExpiresAt > now &&
        CryptographicOperations.FixedTimeEquals(challenge.CodeDigest,
            suppliedDigest);

    public async ValueTask<VerificationIssue?> ResendAsync(
        VerificationPurpose purpose, long authKeyId, long subjectId,
        string expectedPublicHash,
        CancellationToken cancellationToken = default)
    {
        TLVerificationChallenge? found = await _repository
            .GetActiveChallengeAsync((int)purpose, authKeyId, subjectId,
                cancellationToken);
        if (found is not { } challenge)
        {
            return null;
        }

        string destination;
        VerificationChannel channel;
        byte[] context;
        using (challenge)
        {
            var view = challenge.AsVerificationChallenge();
            if (view.ExpiresAt <= UnixNow())
            {
                await _repository.DeleteActiveChallengeAsync((int)purpose,
                    authKeyId, subjectId, cancellationToken);
                return null;
            }
            if (!view.PublicHash.SequenceEqual(
                    Encoding.UTF8.GetBytes(expectedPublicHash)))
            {
                return null;
            }
            destination = Encoding.UTF8.GetString(view.Destination);
            channel = (VerificationChannel)view.Channel;
            context = view.Context.ToArray();
        }

        return channel == VerificationChannel.Email
            ? await IssueEmailAsync(purpose, authKeyId, subjectId, destination,
                context, cancellationToken)
            : await IssueSmsAsync(purpose, authKeyId, subjectId, destination,
                context, cancellationToken);
    }

    public async ValueTask<VerifiedChallenge?> GetActiveAsync(
        VerificationPurpose purpose, long authKeyId, long subjectId,
        string expectedPublicHash,
        CancellationToken cancellationToken = default)
    {
        TLVerificationChallenge? found = await _repository
            .GetActiveChallengeAsync((int)purpose, authKeyId, subjectId,
                cancellationToken);
        if (found is not { } challenge)
        {
            return null;
        }

        using (challenge)
        {
            var view = challenge.AsVerificationChallenge();
            if (view.ExpiresAt <= UnixNow())
            {
                await _repository.DeleteActiveChallengeAsync((int)purpose,
                    authKeyId, subjectId, cancellationToken);
                return null;
            }
            return view.PublicHash.SequenceEqual(
                    Encoding.UTF8.GetBytes(expectedPublicHash))
                ? ToVerifiedChallenge(view)
                : null;
        }
    }

    public async ValueTask<VerifiedChallenge?> VerifyActiveAsync(
        VerificationPurpose purpose, long authKeyId, long subjectId,
        string code, bool consume = true,
        CancellationToken cancellationToken = default)
    {
        TLVerificationChallenge? found = await _repository
            .GetActiveChallengeAsync((int)purpose, authKeyId, subjectId,
                cancellationToken);
        if (found is not { } challenge)
        {
            return null;
        }

        string publicHash;
        using (challenge)
        {
            var view = challenge.AsVerificationChallenge();
            if (view.ExpiresAt <= UnixNow())
            {
                await _repository.DeleteActiveChallengeAsync((int)purpose,
                    authKeyId, subjectId, cancellationToken);
                return null;
            }
            publicHash = Encoding.UTF8.GetString(view.PublicHash);
        }

        return await VerifyAsync(purpose, authKeyId, subjectId, publicHash,
            code, consume, cancellationToken);
    }

    public async ValueTask<bool> ReissueSmsInPlaceAsync(
        VerificationPurpose purpose, long authKeyId, long subjectId,
        string expectedPublicHash,
        CancellationToken cancellationToken = default)
    {
        TLVerificationChallenge? found = await _repository
            .GetActiveChallengeAsync((int)purpose, authKeyId, subjectId,
                cancellationToken);
        if (found is not { } challenge)
        {
            return false;
        }

        long challengeId;
        string destination;
        byte[] publicHash;
        byte[] context;
        using (challenge)
        {
            var view = challenge.AsVerificationChallenge();
            if (view.ExpiresAt <= UnixNow() ||
                view.Channel != (int)VerificationChannel.Sms ||
                !view.PublicHash.SequenceEqual(
                    Encoding.UTF8.GetBytes(expectedPublicHash)))
            {
                return false;
            }
            challengeId = view.ChallengeId;
            destination = Encoding.UTF8.GetString(view.Destination);
            publicHash = view.PublicHash.ToArray();
            context = view.Context.ToArray();
        }

        string code = await _gateway.SendSms(destination);
        int now = UnixNow();
        int expiresAt = checked(now + (int)ChallengeTtl.TotalSeconds);
        byte[] digest = Digest(code);
        try
        {
            using TLVerificationChallenge replacement = VerificationChallenge
                .Builder()
                .ChallengeId(challengeId)
                .Purpose((int)purpose)
                .Channel((int)VerificationChannel.Sms)
                .AuthKeyId(authKeyId)
                .SubjectId(subjectId)
                .Destination(Encoding.UTF8.GetBytes(destination))
                .PublicHash(publicHash)
                .CodeDigest(digest)
                .CreatedAt(now)
                .ExpiresAt(expiresAt)
                .Attempts(0)
                .Context(context)
                .Build();
            await _repository.PutChallengeAsync(replacement, ChallengeTtl,
                cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
        return true;
    }

    public async ValueTask<bool> CancelAsync(VerificationPurpose purpose,
        long authKeyId, long subjectId, string expectedPublicHash,
        CancellationToken cancellationToken = default)
    {
        TLVerificationChallenge? found = await _repository.GetChallengeAsync(
            expectedPublicHash, cancellationToken);
        if (found is not { } challenge)
        {
            return false;
        }
        using (challenge)
        {
            var view = challenge.AsVerificationChallenge();
            if (view.Purpose != (int)purpose || view.AuthKeyId != authKeyId ||
                view.SubjectId != subjectId || view.ExpiresAt <= UnixNow())
            {
                return false;
            }
        }

        TLVerificationChallenge? consumed = await _repository
            .ConsumeChallengeAsync(expectedPublicHash, cancellationToken);
        using (consumed)
        {
            return consumed != null;
        }
    }

    public async ValueTask<bool> ReportMissingAsync(VerificationPurpose purpose,
        long authKeyId, long subjectId, string publicHash,
        CancellationToken cancellationToken = default)
    {
        TLVerificationChallenge? found = await _repository.GetChallengeAsync(
            publicHash, cancellationToken);
        if (found is not { } challenge)
        {
            return false;
        }

        using (challenge)
        {
            var view = challenge.AsVerificationChallenge();
            int remainingSeconds = view.ExpiresAt - UnixNow();
            if (view.Purpose != (int)purpose || view.AuthKeyId != authKeyId ||
                view.SubjectId != subjectId || remainingSeconds <= 0)
            {
                return false;
            }

            using TLVerificationChallenge replacement = view.Clone()
                .ReportedMissing(true)
                .Build();
            await _repository.PutChallengeAsync(replacement,
                TimeSpan.FromSeconds(remainingSeconds), cancellationToken);
            return true;
        }
    }

    public async ValueTask<int> InvalidateByCodesAsync(IEnumerable<string> codes,
        CancellationToken cancellationToken = default)
    {
        int invalidated = 0;
        foreach (string code in codes.Distinct(StringComparer.Ordinal))
        {
            byte[] digest = Digest(code);
            invalidated += await _repository.InvalidateByCodeDigestAsync(digest,
                cancellationToken);
            CryptographicOperations.ZeroMemory(digest);
        }
        return invalidated;
    }

    private async ValueTask<VerificationIssue> PutAsync(
        VerificationPurpose purpose, VerificationChannel channel,
        long authKeyId, long subjectId, string destination, string code,
        ReadOnlyMemory<byte> context, CancellationToken cancellationToken)
    {
        int now = UnixNow();
        int expiresAt = checked(now + (int)ChallengeTtl.TotalSeconds);
        byte[] publicHashBytes = _random.GetRandomBytes(16);
        string publicHash = Convert.ToHexString(publicHashBytes).ToLowerInvariant();
        long challengeId = _random.NextLong() & long.MaxValue;
        if (challengeId == 0)
        {
            challengeId = 1;
        }
        byte[] digest = Digest(code);
        try
        {
            using TLVerificationChallenge challenge = VerificationChallenge.Builder()
                .ChallengeId(challengeId)
                .Purpose((int)purpose)
                .Channel((int)channel)
                .AuthKeyId(authKeyId)
                .SubjectId(subjectId)
                .Destination(Encoding.UTF8.GetBytes(destination))
                .PublicHash(Encoding.UTF8.GetBytes(publicHash))
                .CodeDigest(digest)
                .CreatedAt(now)
                .ExpiresAt(expiresAt)
                .Attempts(0)
                .Context(context.Span)
                .Build();
            await _repository.PutChallengeAsync(challenge, ChallengeTtl,
                cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }

        return new VerificationIssue(publicHash, code.Length, expiresAt);
    }

    private int UnixNow() =>
        checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());

    private static VerifiedChallenge ToVerifiedChallenge(
        VerificationChallenge view) =>
        new(view.ChallengeId, (VerificationPurpose)view.Purpose,
            view.AuthKeyId, view.SubjectId,
            Encoding.UTF8.GetString(view.Destination), view.Context.ToArray());

    private static byte[] Digest(string value) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
