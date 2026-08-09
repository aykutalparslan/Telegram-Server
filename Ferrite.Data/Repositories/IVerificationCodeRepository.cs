// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IVerificationCodeRepository
{
    ValueTask PutChallengeAsync(TLDto.TLVerificationChallenge challenge, TimeSpan ttl,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLVerificationChallenge?> GetChallengeAsync(string publicHash,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLVerificationChallenge?> GetActiveChallengeAsync(int purpose,
        long authKeyId, long subjectId, CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLVerificationChallenge?> ConsumeChallengeAsync(string publicHash,
        CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteActiveChallengeAsync(int purpose, long authKeyId, long subjectId,
        CancellationToken cancellationToken = default);
    ValueTask<int> InvalidateByCodeDigestAsync(ReadOnlyMemory<byte> codeDigest,
        CancellationToken cancellationToken = default);
}
