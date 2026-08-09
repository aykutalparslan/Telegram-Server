// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IAccountPasswordRepository
{
    ValueTask<TLDto.TLAccountPasswordState?> GetPasswordStateAsync(long userId,
        CancellationToken cancellationToken = default);
    ValueTask PutPasswordStateAsync(TLDto.TLAccountPasswordState state,
        CancellationToken cancellationToken = default);
    ValueTask<bool> DeletePasswordStateAsync(long userId,
        CancellationToken cancellationToken = default);

    ValueTask PutSrpChallengeAsync(TLDto.TLPasswordSrpChallenge challenge, TimeSpan ttl,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLPasswordSrpChallenge?> GetSrpChallengeAsync(long srpId,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLPasswordSrpChallenge?> ConsumeSrpChallengeAsync(long srpId,
        CancellationToken cancellationToken = default);

    ValueTask PutTemporaryPasswordAsync(TLDto.TLTemporaryPasswordState password,
        TimeSpan ttl, CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLTemporaryPasswordState?> GetTemporaryPasswordAsync(long userId,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLTemporaryPasswordState?> ConsumeTemporaryPasswordAsync(long userId,
        CancellationToken cancellationToken = default);

    ValueTask<TLDto.TLPasswordResetState?> GetResetStateAsync(long userId,
        CancellationToken cancellationToken = default);
    ValueTask PutResetStateAsync(TLDto.TLPasswordResetState state,
        CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteResetStateAsync(long userId,
        CancellationToken cancellationToken = default);
}
