// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class VerificationCodeRepository : IVerificationCodeRepository
{
    private readonly IVolatileKVStore _challenges;
    private readonly IVolatileKVStore _activeChallenges;
    private readonly IVolatileKVStore _byCodeDigest;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public VerificationCodeRepository(IVolatileKVStore challenges,
        IVolatileKVStore activeChallenges, IVolatileKVStore byCodeDigest)
    {
        _challenges = challenges;
        challenges.SetSchema(new TableDefinition("ferrite", "verification_challenges",
            new KeyDefinition("pk",
                new DataColumn { Name = "public_hash", Type = DataType.String })));
        _activeChallenges = activeChallenges;
        activeChallenges.SetSchema(new TableDefinition("ferrite",
            "active_verification_challenges",
            new KeyDefinition("pk",
                new DataColumn { Name = "purpose", Type = DataType.Int },
                new DataColumn { Name = "auth_key_id", Type = DataType.Long },
                new DataColumn { Name = "subject_id", Type = DataType.Long })));
        _byCodeDigest = byCodeDigest;
        byCodeDigest.SetSchema(new TableDefinition("ferrite",
            "verification_challenges_by_code",
            new KeyDefinition("pk",
                new DataColumn { Name = "code_digest", Type = DataType.Bytes })));
    }

    private void DeleteIndexes(byte[] bytes)
    {
        var view = new TLDto.TLVerificationChallenge(bytes, 0, bytes.Length)
            .AsVerificationChallenge();
        string publicHash = Encoding.UTF8.GetString(view.PublicHash);
        byte[] publicHashBytes = view.PublicHash.ToArray();
        byte[] codeDigest = view.CodeDigest.ToArray();
        byte[]? active = _activeChallenges.Get(view.Purpose, view.AuthKeyId, view.SubjectId);
        if (active != null && active.AsSpan().SequenceEqual(publicHashBytes))
        {
            _activeChallenges.Delete(view.Purpose, view.AuthKeyId, view.SubjectId);
        }
        _byCodeDigest.ListDelete(publicHashBytes, codeDigest);
        _challenges.Delete(publicHash);
    }

    public async ValueTask PutChallengeAsync(TLDto.TLVerificationChallenge challenge,
        TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var view = challenge.AsVerificationChallenge();
        int purpose = view.Purpose;
        long authKeyId = view.AuthKeyId;
        long subjectId = view.SubjectId;
        int expiresAt = view.ExpiresAt;
        byte[] publicHashBytes = view.PublicHash.ToArray();
        string publicHash = Encoding.UTF8.GetString(publicHashBytes);
        byte[] codeDigest = view.CodeDigest.ToArray();
        byte[] bytes = challenge.AsSpan().ToArray();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            byte[]? oldActiveHash = _activeChallenges.Get(purpose, authKeyId, subjectId);
            if (oldActiveHash != null)
            {
                byte[]? oldActive = _challenges.Get(Encoding.UTF8.GetString(oldActiveHash));
                if (oldActive != null)
                {
                    DeleteIndexes(oldActive);
                }
                else
                {
                    _activeChallenges.Delete(purpose, authKeyId, subjectId);
                }
            }
            byte[]? oldHash = _challenges.Get(publicHash);
            if (oldHash != null)
            {
                DeleteIndexes(oldHash);
            }

            _challenges.Put(bytes, ttl, publicHash);
            _activeChallenges.Put(publicHashBytes, ttl, purpose, authKeyId, subjectId);
            _byCodeDigest.ListAdd((long)expiresAt * 1000, publicHashBytes, ttl, codeDigest);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<TLDto.TLVerificationChallenge?> GetChallengeAsync(
        string publicHash, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            byte[]? bytes = _challenges.Get(publicHash);
            return bytes == null
                ? null
                : new TLDto.TLVerificationChallenge(bytes, 0, bytes.Length);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<TLDto.TLVerificationChallenge?> GetActiveChallengeAsync(
        int purpose, long authKeyId, long subjectId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            byte[]? publicHash = _activeChallenges.Get(purpose, authKeyId, subjectId);
            if (publicHash == null)
            {
                return null;
            }
            byte[]? bytes = _challenges.Get(Encoding.UTF8.GetString(publicHash));
            if (bytes == null)
            {
                _activeChallenges.Delete(purpose, authKeyId, subjectId);
                return null;
            }
            return new TLDto.TLVerificationChallenge(bytes, 0, bytes.Length);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<TLDto.TLVerificationChallenge?> ConsumeChallengeAsync(
        string publicHash, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            byte[]? bytes = _challenges.Get(publicHash);
            if (bytes == null)
            {
                return null;
            }
            DeleteIndexes(bytes);
            return new TLDto.TLVerificationChallenge(bytes, 0, bytes.Length);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<bool> DeleteActiveChallengeAsync(int purpose, long authKeyId,
        long subjectId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            byte[]? publicHash = _activeChallenges.Get(purpose, authKeyId, subjectId);
            if (publicHash == null)
            {
                return false;
            }
            byte[]? bytes = _challenges.Get(Encoding.UTF8.GetString(publicHash));
            if (bytes != null)
            {
                DeleteIndexes(bytes);
            }
            else
            {
                _activeChallenges.Delete(purpose, authKeyId, subjectId);
            }
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<int> InvalidateByCodeDigestAsync(
        ReadOnlyMemory<byte> codeDigest, CancellationToken cancellationToken = default)
    {
        byte[] digest = codeDigest.ToArray();
        await _gate.WaitAsync(cancellationToken);
        try
        {
            int invalidated = 0;
            foreach (byte[] publicHash in _byCodeDigest.ListGet(digest)
                         .Distinct(ByteArrayComparer.Instance).ToArray())
            {
                byte[]? bytes = _challenges.Get(Encoding.UTF8.GetString(publicHash));
                if (bytes == null)
                {
                    continue;
                }
                var view = new TLDto.TLVerificationChallenge(bytes, 0, bytes.Length)
                    .AsVerificationChallenge();
                if (!view.CodeDigest.SequenceEqual(digest))
                {
                    continue;
                }
                DeleteIndexes(bytes);
                invalidated++;
            }
            _byCodeDigest.Delete(digest);
            return invalidated;
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public static readonly ByteArrayComparer Instance = new();

        public bool Equals(byte[]? x, byte[]? y) =>
            ReferenceEquals(x, y) || x != null && y != null && x.AsSpan().SequenceEqual(y);

        public int GetHashCode(byte[] obj)
        {
            HashCode hash = new();
            foreach (byte value in obj)
            {
                hash.Add(value);
            }
            return hash.ToHashCode();
        }
    }
}
